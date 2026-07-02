namespace NanoVault.Core.Models;

/// <summary>A playlist recovered read-only from the iPod database.</summary>
public sealed record IpodPlaylist
{
    public required string Name { get; init; }

    /// <summary>
    /// Source-relative paths (relative to the iPod root) of the member tracks,
    /// in playlist order. Missing tracks are skipped when exporting.
    /// </summary>
    public IReadOnlyList<string> TrackRelativePaths { get; init; } = Array.Empty<string>();
}

public sealed record ScanWarning(string Message, string? Path = null, WarningSeverity Severity = WarningSeverity.Warning);

/// <summary>Progress raised while scanning the iPod.</summary>
public readonly record struct ScanProgress(int FilesSeen, int FilesTotalEstimate, string? CurrentFile);

/// <summary>Everything learned from a read-only scan of one iPod.</summary>
public sealed record ScanResult
{
    public required IpodDevice Device { get; init; }
    public IReadOnlyList<DiscoveredTrack> Tracks { get; init; } = Array.Empty<DiscoveredTrack>();
    public IReadOnlyList<IpodPlaylist> Playlists { get; init; } = Array.Empty<IpodPlaylist>();
    public IReadOnlyList<ScanWarning> Warnings { get; init; } = Array.Empty<ScanWarning>();

    public long TotalBytes => Tracks.Sum(t => t.SizeBytes);
    public int ReadableCount => Tracks.Count(t => t.Status == TrackReadStatus.Readable);
    public int ProtectedCount => Tracks.Count(t => t.Status == TrackReadStatus.Protected);
    public int UnreadableCount => Tracks.Count(t => t.Status == TrackReadStatus.Unreadable);
}
