using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Policies;

namespace NanoVault.Ipod.Database;

/// <summary>
/// Read-only parser for the classic iPod database (iTunesDB / iTunesCDB).
///
/// The format is a tree of little-endian chunks. Each chunk starts with a
/// 4-byte ASCII id, a header length, and (for most chunks) a total length:
///   mhbd → mhsd (type 1: tracks → mhlt → mhit → mhod,
///                type 2/3: playlists → mhlp → mhyp → mhod/mhip)
///
/// Every offset and length is validated before use. A malformed record fails
/// individually and is logged; it never fails the whole scan. Nothing is ever
/// written to the database.
/// </summary>
public sealed class ITunesDbReader : IIpodDatabaseReader
{
    private const int MaxDatabaseBytes = 512 * 1024 * 1024;
    private const int MaxStringBytes = 64 * 1024;

    // mhod string types we care about.
    private const uint MhodTitle = 1;
    private const uint MhodLocation = 2;
    private const uint MhodAlbum = 3;
    private const uint MhodArtist = 4;
    private const uint MhodGenre = 5;
    private const uint MhodComposer = 12;
    private const uint MhodAlbumArtist = 22;

    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly ILogger<ITunesDbReader> _logger;

    public ITunesDbReader(IReadOnlyFileSystem fileSystem, ILogger<ITunesDbReader> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, IpodTrackRecord>> ReadTracksAsync(
        string ipodRoot,
        CancellationToken cancellationToken)
    {
        var parsed = await LoadAndParseAsync(ipodRoot, cancellationToken).ConfigureAwait(false);
        return parsed.TracksByPathKey;
    }

    public async Task<IReadOnlyList<IpodPlaylistRecord>> ReadPlaylistsAsync(
        string ipodRoot,
        CancellationToken cancellationToken)
    {
        var parsed = await LoadAndParseAsync(ipodRoot, cancellationToken).ConfigureAwait(false);
        return parsed.Playlists;
    }

    private sealed record ParsedDatabase(
        IReadOnlyDictionary<string, IpodTrackRecord> TracksByPathKey,
        IReadOnlyList<IpodPlaylistRecord> Playlists)
    {
        public static ParsedDatabase Empty { get; } = new(
            new Dictionary<string, IpodTrackRecord>(),
            Array.Empty<IpodPlaylistRecord>());
    }

    private async Task<ParsedDatabase> LoadAndParseAsync(string ipodRoot, CancellationToken cancellationToken)
    {
        try
        {
            var databasePath = LocateDatabase(ipodRoot);
            if (databasePath is null)
            {
                _logger.LogInformation("No iPod database found under {Root}; embedded tags only", ipodRoot);
                return ParsedDatabase.Empty;
            }

            var data = await ReadAllBytesAsync(databasePath, cancellationToken).ConfigureAwait(false);
            if (data is null)
            {
                return ParsedDatabase.Empty;
            }

            data = MaybeDecompress(data);
            return Parse(data, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "iPod database could not be read; falling back to embedded tags only");
            return ParsedDatabase.Empty;
        }
    }

    private string? LocateDatabase(string ipodRoot)
    {
        var control = CaseInsensitivePath.FindDirectory(_fileSystem, ipodRoot, IpodDiscoveryService.IpodControlFolderName);
        if (control is null)
        {
            return null;
        }

        var itunes = CaseInsensitivePath.FindDirectory(_fileSystem, control, IpodDiscoveryService.ITunesFolderName);
        if (itunes is null)
        {
            return null;
        }

        return CaseInsensitivePath.FindFile(_fileSystem, itunes, "iTunesDB")
            ?? CaseInsensitivePath.FindFile(_fileSystem, itunes, "iTunesCDB");
    }

    private async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        var size = _fileSystem.GetFileSize(path);
        if (size <= 0 || size > MaxDatabaseBytes)
        {
            _logger.LogWarning("iPod database has an implausible size ({Size} bytes); skipping", size);
            return null;
        }

        await using var stream = _fileSystem.OpenRead(path);
        using var buffer = new MemoryStream(capacity: (int)size);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>iTunesCDB stores a zlib stream after the mhbd header. Read-only inflate.</summary>
    private byte[] MaybeDecompress(byte[] data)
    {
        if (data.Length < 12 || !HasChunkId(data, 0, "mhbd"))
        {
            return data;
        }

        var headerLen = (int)ReadU32(data, 4);
        if (headerLen <= 0 || headerLen >= data.Length || data[headerLen] != 0x78)
        {
            return data;
        }

        try
        {
            using var compressed = new MemoryStream(data, headerLen, data.Length - headerLen, writable: false);
            using var inflate = new ZLibStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream();
            output.Write(data, 0, headerLen);
            inflate.CopyTo(output);
            _logger.LogInformation("Decompressed iTunesCDB payload in memory");
            return output.ToArray();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            // Not actually compressed; parse the raw bytes instead.
            return data;
        }
    }

    private ParsedDatabase Parse(byte[] data, CancellationToken cancellationToken)
    {
        var tracksById = new Dictionary<uint, IpodTrackRecord>();
        var playlists = new List<IpodPlaylistRecord>();
        var warningCount = 0;

        if (!HasChunkId(data, 0, "mhbd"))
        {
            _logger.LogWarning("iPod database does not start with the expected mhbd header; skipping");
            return ParsedDatabase.Empty;
        }

        var mhbdHeaderLen = (int)ReadU32(data, 4);
        if (mhbdHeaderLen < 12 || mhbdHeaderLen > data.Length)
        {
            _logger.LogWarning("iPod database mhbd header length is invalid; skipping");
            return ParsedDatabase.Empty;
        }

        var rawPlaylists = new List<(string Name, bool IsMaster, List<uint> TrackIds)>();

        var offset = mhbdHeaderLen;
        while (offset + 12 <= data.Length && HasChunkId(data, offset, "mhsd"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var headerLen = (int)ReadU32(data, offset + 4);
            var totalLen = (int)ReadU32(data, offset + 8);
            var type = ReadU32(data, offset + 12);

            if (headerLen < 16 || totalLen < headerLen || offset + totalLen > data.Length)
            {
                _logger.LogWarning("Skipping malformed mhsd section at offset {Offset}", offset);
                break;
            }

            var sectionStart = offset + headerLen;
            var sectionEnd = offset + totalLen;

            try
            {
                switch (type)
                {
                    case 1:
                        ParseTrackList(data, sectionStart, sectionEnd, tracksById, ref warningCount, cancellationToken);
                        break;
                    case 2:
                    case 3:
                        ParsePlaylistList(data, sectionStart, sectionEnd, rawPlaylists, ref warningCount, cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warningCount++;
                _logger.LogWarning(ex, "Failed to parse mhsd section type {Type}", type);
            }

            offset = sectionEnd;
        }

        // Resolve playlist member ids to source-relative paths.
        foreach (var (name, isMaster, trackIds) in rawPlaylists)
        {
            var paths = new List<string>(trackIds.Count);
            foreach (var id in trackIds)
            {
                if (tracksById.TryGetValue(id, out var track) && track.RelativePath is not null)
                {
                    paths.Add(track.RelativePath);
                }
            }

            playlists.Add(new IpodPlaylistRecord
            {
                Name = name,
                IsMaster = isMaster,
                TrackRelativePaths = paths,
            });
        }

        var byPath = new Dictionary<string, IpodTrackRecord>(StringComparer.Ordinal);
        foreach (var track in tracksById.Values)
        {
            if (track.RelativePath is not null)
            {
                byPath[IpodPathNormalizer.ToComparableKey(track.RelativePath)] = track;
            }
        }

        if (warningCount > 0)
        {
            _logger.LogWarning("iPod database parsed with {Count} recoverable warnings", warningCount);
        }

        _logger.LogInformation(
            "iPod database: {Tracks} tracks, {Playlists} playlists recovered",
            byPath.Count, playlists.Count);

        return new ParsedDatabase(byPath, playlists);
    }

    private void ParseTrackList(
        byte[] data,
        int start,
        int end,
        Dictionary<uint, IpodTrackRecord> tracksById,
        ref int warningCount,
        CancellationToken cancellationToken)
    {
        if (start + 12 > end || !HasChunkId(data, start, "mhlt"))
        {
            warningCount++;
            _logger.LogWarning("Track section does not contain an mhlt list");
            return;
        }

        var headerLen = (int)ReadU32(data, start + 4);
        if (headerLen < 12 || start + headerLen > end)
        {
            warningCount++;
            return;
        }

        var offset = start + headerLen;
        while (offset + 12 <= end && HasChunkId(data, offset, "mhit"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var advance = ParseTrack(data, offset, end, tracksById, ref warningCount);
            if (advance <= 0)
            {
                break;
            }

            offset += advance;
        }
    }

    /// <summary>Parses one mhit. Returns bytes to advance, or 0 to stop.</summary>
    private int ParseTrack(
        byte[] data,
        int offset,
        int end,
        Dictionary<uint, IpodTrackRecord> tracksById,
        ref int warningCount)
    {
        var headerLen = (int)ReadU32(data, offset + 4);
        var totalLen = (int)ReadU32(data, offset + 8);

        if (headerLen < 0x14 || totalLen < headerLen || offset + totalLen > end)
        {
            warningCount++;
            _logger.LogWarning("Skipping malformed track record at offset {Offset}", offset);
            return 0;
        }

        try
        {
            var mhodCount = ReadU32(data, offset + 0x0C);
            var id = ReadU32(data, offset + 0x10);

            int? ReadField(int fieldOffset)
            {
                if (fieldOffset + 4 > headerLen)
                {
                    return null;
                }

                var value = ReadU32(data, offset + fieldOffset);
                return value is > 0 and <= int.MaxValue ? (int)value : null;
            }

            var sizeBytes = ReadField(0x24);
            var durationMs = ReadField(0x28);
            var trackNumber = ReadField(0x2C);
            var trackCount = ReadField(0x30);
            var year = ReadField(0x34);
            var bitrate = ReadField(0x38);
            var discNumber = ReadField(0x60);
            var discCount = ReadField(0x64);

            // Sample rate is stored as a 16.16 fixed-point value in most
            // versions, so read it unsigned before deciding how to scale.
            int? sampleRate = null;
            if (headerLen >= 0x40)
            {
                var rawRate = ReadU32(data, offset + 0x3C);
                var scaled = rawRate > 1_000_000 ? rawRate >> 16 : rawRate;
                if (scaled is > 0 and <= int.MaxValue)
                {
                    sampleRate = (int)scaled;
                }
            }

            string? title = null, artist = null, albumArtist = null, album = null;
            string? genre = null, composer = null, location = null;

            var mhodOffset = offset + headerLen;
            var trackEnd = offset + totalLen;
            for (var i = 0; i < mhodCount && mhodOffset + 12 <= trackEnd; i++)
            {
                var (type, text, advance) = ParseMhod(data, mhodOffset, trackEnd);
                if (advance <= 0)
                {
                    break;
                }

                switch (type)
                {
                    case MhodTitle: title ??= text; break;
                    case MhodLocation: location ??= text; break;
                    case MhodAlbum: album ??= text; break;
                    case MhodArtist: artist ??= text; break;
                    case MhodGenre: genre ??= text; break;
                    case MhodComposer: composer ??= text; break;
                    case MhodAlbumArtist: albumArtist ??= text; break;
                }

                mhodOffset += advance;
            }

            var relativePath = IpodPathNormalizer.ToRelativePath(location);

            // Sanity-check year and numbers so garbage never reaches file names.
            if (year is < 1900 or > 2100)
            {
                year = null;
            }

            if (trackNumber is > 999)
            {
                trackNumber = null;
            }

            if (discNumber is > 99)
            {
                discNumber = null;
            }

            tracksById[id] = new IpodTrackRecord
            {
                Id = id,
                Title = title,
                Artist = artist,
                AlbumArtist = albumArtist,
                Album = album,
                Genre = genre,
                Composer = composer,
                TrackNumber = trackNumber,
                TrackCount = trackCount,
                DiscNumber = discNumber,
                DiscCount = discCount,
                Year = year,
                BitrateKbps = bitrate,
                SampleRateHz = sampleRate,
                DurationMs = durationMs,
                SizeBytes = sizeBytes,
                RelativePath = relativePath,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or DecoderFallbackException)
        {
            warningCount++;
            _logger.LogWarning(ex, "Skipping unreadable track record at offset {Offset}", offset);
        }

        return totalLen;
    }

    private void ParsePlaylistList(
        byte[] data,
        int start,
        int end,
        List<(string Name, bool IsMaster, List<uint> TrackIds)> playlists,
        ref int warningCount,
        CancellationToken cancellationToken)
    {
        if (start + 12 > end || !HasChunkId(data, start, "mhlp"))
        {
            return;
        }

        var headerLen = (int)ReadU32(data, start + 4);
        if (headerLen < 12 || start + headerLen > end)
        {
            warningCount++;
            return;
        }

        var offset = start + headerLen;
        var index = 0;
        while (offset + 12 <= end && HasChunkId(data, offset, "mhyp"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var advance = ParsePlaylist(data, offset, end, index, playlists, ref warningCount);
            if (advance <= 0)
            {
                break;
            }

            offset += advance;
            index++;
        }
    }

    private int ParsePlaylist(
        byte[] data,
        int offset,
        int end,
        int index,
        List<(string Name, bool IsMaster, List<uint> TrackIds)> playlists,
        ref int warningCount)
    {
        var headerLen = (int)ReadU32(data, offset + 4);
        var totalLen = (int)ReadU32(data, offset + 8);

        if (headerLen < 0x18 || totalLen < headerLen || offset + totalLen > end)
        {
            warningCount++;
            _logger.LogWarning("Skipping malformed playlist record at offset {Offset}", offset);
            return 0;
        }

        var isMaster = headerLen > 0x14 && data[offset + 0x14] != 0;

        string? name = null;
        var trackIds = new List<uint>();

        var childOffset = offset + headerLen;
        var playlistEnd = offset + totalLen;

        while (childOffset + 12 <= playlistEnd)
        {
            if (HasChunkId(data, childOffset, "mhod"))
            {
                var (type, text, advance) = ParseMhod(data, childOffset, playlistEnd);
                if (advance <= 0)
                {
                    break;
                }

                if (type == MhodTitle && !string.IsNullOrWhiteSpace(text))
                {
                    name ??= text;
                }

                childOffset += advance;
            }
            else if (HasChunkId(data, childOffset, "mhip"))
            {
                var itemHeaderLen = (int)ReadU32(data, childOffset + 4);
                var itemTotalLen = (int)ReadU32(data, childOffset + 8);

                if (itemHeaderLen < 0x1C || itemTotalLen < itemHeaderLen || childOffset + itemTotalLen > playlistEnd)
                {
                    warningCount++;
                    break;
                }

                trackIds.Add(ReadU32(data, childOffset + 0x18));
                childOffset += itemTotalLen;
            }
            else
            {
                break;
            }
        }

        playlists.Add((name ?? $"Playlist {index + 1}", isMaster, trackIds));
        return totalLen;
    }

    /// <summary>
    /// Parses one mhod. Returns its type, decoded string when it is a string
    /// mhod, and the byte advance (0 when the chunk is malformed).
    /// </summary>
    private (uint Type, string? Text, int Advance) ParseMhod(byte[] data, int offset, int end)
    {
        if (offset + 16 > end || !HasChunkId(data, offset, "mhod"))
        {
            return (0, null, 0);
        }

        var headerLen = (int)ReadU32(data, offset + 4);
        var totalLen = (int)ReadU32(data, offset + 8);
        var type = ReadU32(data, offset + 12);

        if (headerLen < 0x18 || totalLen < headerLen || offset + totalLen > end)
        {
            return (0, null, 0);
        }

        // Only classic string mhods carry text we use (types 1–14 and 22).
        if (type is not (>= 1 and <= 14 or MhodAlbumArtist))
        {
            return (type, null, totalLen);
        }

        var stringHeader = offset + headerLen;
        if (stringHeader + 16 > offset + totalLen)
        {
            return (type, null, totalLen);
        }

        var byteLength = (int)ReadU32(data, stringHeader + 4);
        var textStart = stringHeader + 16;

        if (byteLength <= 0
            || byteLength > MaxStringBytes
            || byteLength % 2 != 0
            || textStart + byteLength > offset + totalLen)
        {
            return (type, null, totalLen);
        }

        try
        {
            var text = Encoding.Unicode.GetString(data, textStart, byteLength).TrimEnd('\0').Trim();
            return (type, text.Length == 0 ? null : text, totalLen);
        }
        catch (ArgumentException)
        {
            return (type, null, totalLen);
        }
    }

    private static uint ReadU32(byte[] data, int offset) =>
        offset >= 0 && offset + 4 <= data.Length
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
            : 0;

    private static bool HasChunkId(byte[] data, int offset, string id) =>
        offset >= 0
        && offset + 4 <= data.Length
        && data[offset] == (byte)id[0]
        && data[offset + 1] == (byte)id[1]
        && data[offset + 2] == (byte)id[2]
        && data[offset + 3] == (byte)id[3];
}
