using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.Infrastructure.Metadata;

/// <summary>
/// Reads embedded tags with TagLibSharp through a strictly read-only file
/// abstraction: any attempt by the library to open a write stream throws,
/// so tag reading can never modify the iPod.
/// </summary>
public sealed class TagLibAudioMetadataReader : IAudioMetadataReader
{
    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly ILogger<TagLibAudioMetadataReader> _logger;

    public TagLibAudioMetadataReader(IReadOnlyFileSystem fileSystem, ILogger<TagLibAudioMetadataReader> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<MetadataReadResult> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var abstraction = new ReadOnlyFileAbstraction(_fileSystem, path);
                using var file = TagLib.File.Create(abstraction, TagLib.ReadStyle.Average);

                var tag = file.Tag;
                var properties = file.Properties;

                var metadata = new TrackMetadata
                {
                    Title = Clean(tag.Title),
                    Artist = Clean(tag.JoinedPerformers),
                    AlbumArtist = Clean(tag.JoinedAlbumArtists),
                    Album = Clean(tag.Album),
                    Genre = Clean(tag.FirstGenre),
                    Composer = Clean(tag.FirstComposer),
                    TrackNumber = PositiveOrNull((int)tag.Track),
                    TrackCount = PositiveOrNull((int)tag.TrackCount),
                    DiscNumber = PositiveOrNull((int)tag.Disc),
                    DiscCount = PositiveOrNull((int)tag.DiscCount),
                    Year = tag.Year is > 1900 and < 2100 ? (int)tag.Year : null,
                    Duration = properties?.Duration > TimeSpan.Zero ? properties.Duration : null,
                    BitrateKbps = PositiveOrNull(properties?.AudioBitrate ?? 0),
                    SampleRateHz = PositiveOrNull(properties?.AudioSampleRate ?? 0),
                    HasArtwork = tag.Pictures is { Length: > 0 },
                    Sources = MetadataSources.EmbeddedTags,
                };

                return new MetadataReadResult { Metadata = metadata };
            }
            catch (Exception ex) when (ex is TagLib.CorruptFileException
                                        or TagLib.UnsupportedFormatException
                                        or IOException
                                        or UnauthorizedAccessException
                                        or ArgumentException)
            {
                _logger.LogDebug(ex, "No embedded tags readable from {Path}", path);
                return new MetadataReadResult { Error = ex.Message };
            }
        }, cancellationToken);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? PositiveOrNull(int value) => value > 0 ? value : null;

    /// <summary>TagLib file abstraction that can only ever read.</summary>
    private sealed class ReadOnlyFileAbstraction : TagLib.File.IFileAbstraction, IDisposable
    {
        private readonly IReadOnlyFileSystem _fileSystem;
        private Stream? _stream;

        public ReadOnlyFileAbstraction(IReadOnlyFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            Name = path;
        }

        public string Name { get; }

        public Stream ReadStream => _stream ??= _fileSystem.OpenRead(Name);

        public Stream WriteStream =>
            throw new NotSupportedException("NanoVault never writes to the iPod.");

        public void CloseStream(Stream stream)
        {
            stream.Dispose();
            if (ReferenceEquals(stream, _stream))
            {
                _stream = null;
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
