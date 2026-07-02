using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Policies;

namespace NanoVault.Ipod;

/// <summary>
/// Read-only scan of a detected iPod: finds every supported audio file under
/// iPod_Control\Music, reads embedded tags, merges the iPod database as a
/// fallback layer, and classifies protected or unreadable files. A single bad
/// file never fails the scan.
/// </summary>
public sealed class IpodScanner : IIpodScanner
{
    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly IAudioMetadataReader _metadataReader;
    private readonly IIpodDatabaseReader _databaseReader;
    private readonly ILogger<IpodScanner> _logger;

    public IpodScanner(
        IReadOnlyFileSystem fileSystem,
        IAudioMetadataReader metadataReader,
        IIpodDatabaseReader databaseReader,
        ILogger<IpodScanner> logger)
    {
        _fileSystem = fileSystem;
        _metadataReader = metadataReader;
        _databaseReader = databaseReader;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(
        IpodDevice device,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<ScanWarning>();
        var tracks = new List<DiscoveredTrack>();

        var musicRoot = LocateMusicFolder(device.RootPath);
        if (musicRoot is null)
        {
            warnings.Add(new ScanWarning(
                "The iPod's music folder could not be found. The device may need disk use enabled.",
                device.RootPath,
                WarningSeverity.Error));
            return new ScanResult { Device = device, Warnings = warnings };
        }

        // Layer 2 first, so lookups are ready while files are processed.
        IReadOnlyDictionary<string, IpodTrackRecord> databaseTracks;
        IReadOnlyList<IpodPlaylistRecord> databasePlaylists;
        try
        {
            databaseTracks = await _databaseReader.ReadTracksAsync(device.RootPath, cancellationToken).ConfigureAwait(false);
            databasePlaylists = await _databaseReader.ReadPlaylistsAsync(device.RootPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "iPod database unavailable; continuing with embedded tags only");
            warnings.Add(new ScanWarning(
                "The iPod's own track list could not be read, so some names may come from the files themselves.",
                Severity: WarningSeverity.Info));
            databaseTracks = new Dictionary<string, IpodTrackRecord>();
            databasePlaylists = Array.Empty<IpodPlaylistRecord>();
        }

        var files = ListCandidateFiles(musicRoot, warnings);
        var total = files.Count;
        var seen = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            seen++;
            progress?.Report(new ScanProgress(seen, total, Path.GetFileName(file)));

            var track = await InspectFileAsync(device, file, databaseTracks, warnings, cancellationToken).ConfigureAwait(false);
            if (track is not null)
            {
                tracks.Add(track);
            }
        }

        var playlists = MapPlaylists(databasePlaylists, tracks);

        _logger.LogInformation(
            "Scan of {Device} complete: {Count} tracks ({Protected} protected, {Unreadable} unreadable), {Playlists} playlists",
            device.DisplayName, tracks.Count, tracks.Count(t => t.Status == TrackReadStatus.Protected),
            tracks.Count(t => t.Status == TrackReadStatus.Unreadable), playlists.Count);

        return new ScanResult
        {
            Device = device,
            Tracks = tracks,
            Playlists = playlists,
            Warnings = warnings,
        };
    }

    private string? LocateMusicFolder(string rootPath)
    {
        var control = CaseInsensitivePath.FindDirectory(_fileSystem, rootPath, IpodDiscoveryService.IpodControlFolderName);
        return control is null
            ? null
            : CaseInsensitivePath.FindDirectory(_fileSystem, control, IpodDiscoveryService.MusicFolderName);
    }

    private List<string> ListCandidateFiles(string musicRoot, List<ScanWarning> warnings)
    {
        var files = new List<string>();
        try
        {
            foreach (var file in _fileSystem.EnumerateFiles(musicRoot, recursive: true))
            {
                var extension = Path.GetExtension(file);
                if (SupportedMedia.IsSupported(extension))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Music folder enumeration was interrupted");
            warnings.Add(new ScanWarning(
                "Some folders on the iPod could not be listed; the scan may be incomplete.",
                musicRoot));
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private async Task<DiscoveredTrack?> InspectFileAsync(
        IpodDevice device,
        string file,
        IReadOnlyDictionary<string, IpodTrackRecord> databaseTracks,
        List<ScanWarning> warnings,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();

        long size;
        DateTime createdUtc;
        DateTime modifiedUtc;
        try
        {
            size = _fileSystem.GetFileSize(file);
            createdUtc = _fileSystem.GetCreationTimeUtc(file);
            modifiedUtc = _fileSystem.GetLastWriteTimeUtc(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "File disappeared or is unreadable during scan: {File}", file);
            warnings.Add(new ScanWarning("A file on the iPod could not be read.", file));
            return null;
        }

        var relative = Path.GetRelativePath(device.RootPath, file);
        var isProtected = SupportedMedia.IsLikelyProtected(extension);

        TrackMetadata? embedded = null;
        var status = isProtected ? TrackReadStatus.Protected : TrackReadStatus.Readable;

        if (!isProtected)
        {
            var read = await _metadataReader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            if (read.Succeeded)
            {
                embedded = read.Metadata;
            }
            else if (!CanOpenForReading(file))
            {
                status = TrackReadStatus.Unreadable;
                warnings.Add(new ScanWarning("This track could not be read.", file));
            }
            // else: tags unreadable but bytes fine — database and fallback naming cover it.
        }
        else if (!CanOpenForReading(file))
        {
            status = TrackReadStatus.Unreadable;
            warnings.Add(new ScanWarning("This protected track could not be read.", file));
        }

        databaseTracks.TryGetValue(IpodPathNormalizer.ToComparableKey(relative), out var databaseRecord);
        var metadata = MetadataMerger.Merge(embedded, databaseRecord);

        if (metadata.Format is null)
        {
            metadata = metadata with { Format = SupportedMedia.FormatLabel(extension) };
        }

        return new DiscoveredTrack
        {
            SourcePath = file,
            RelativeSourcePath = relative,
            SizeBytes = size,
            Extension = extension,
            Status = status,
            Metadata = metadata,
            CreatedUtc = createdUtc,
            ModifiedUtc = modifiedUtc,
        };
    }

    private bool CanOpenForReading(string file)
    {
        try
        {
            using var stream = _fileSystem.OpenRead(file);
            var buffer = new byte[1];
            _ = stream.Read(buffer, 0, 1);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static IReadOnlyList<IpodPlaylist> MapPlaylists(
        IReadOnlyList<IpodPlaylistRecord> records,
        IReadOnlyList<DiscoveredTrack> tracks)
    {
        if (records.Count == 0)
        {
            return Array.Empty<IpodPlaylist>();
        }

        // Only reference tracks that were actually discovered on disk.
        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var track in tracks)
        {
            known[IpodPathNormalizer.ToComparableKey(track.RelativeSourcePath)] = track.RelativeSourcePath;
        }

        var playlists = new List<IpodPlaylist>();
        foreach (var record in records)
        {
            // The master playlist is the whole library; NanoVault generates
            // "All iPod Music.m3u8" itself instead.
            if (record.IsMaster)
            {
                continue;
            }

            var members = record.TrackRelativePaths
                .Select(p => known.GetValueOrDefault(IpodPathNormalizer.ToComparableKey(p)))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();

            if (members.Count > 0)
            {
                playlists.Add(new IpodPlaylist { Name = record.Name, TrackRelativePaths = members });
            }
        }

        return playlists;
    }
}
