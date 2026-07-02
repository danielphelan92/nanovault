using NanoVault.Core.Models;

namespace NanoVault.Core.Abstractions;

/// <summary>Result of trying to read embedded tags from one file.</summary>
public sealed record MetadataReadResult
{
    public TrackMetadata? Metadata { get; init; }
    public bool Succeeded => Metadata is not null;
    public string? Error { get; init; }
}

/// <summary>Reads embedded audio tags (ID3, MP4 atoms, …) from a single file.</summary>
public interface IAudioMetadataReader
{
    Task<MetadataReadResult> ReadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only reader for the iPod's own database files under iPod_Control\iTunes.
/// Must be defensive: malformed records fail individually, never the whole scan.
/// </summary>
public interface IIpodDatabaseReader
{
    /// <summary>
    /// Track records keyed by normalised source-relative path
    /// (lower-case, local separators).
    /// </summary>
    Task<IReadOnlyDictionary<string, IpodTrackRecord>> ReadTracksAsync(
        string ipodRoot,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IpodPlaylistRecord>> ReadPlaylistsAsync(
        string ipodRoot,
        CancellationToken cancellationToken);
}

/// <summary>Scans a detected iPod for music, merging all metadata layers.</summary>
public interface IIpodScanner
{
    Task<ScanResult> ScanAsync(
        IpodDevice device,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
