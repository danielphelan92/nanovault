namespace NanoVault.Core.Models;

/// <summary>
/// One track record recovered read-only from the iPod's own database
/// (iTunesDB). Used as a metadata fallback when embedded tags are missing.
/// </summary>
public sealed record IpodTrackRecord
{
    public uint Id { get; init; }

    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumArtist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public string? Composer { get; init; }

    public int? TrackNumber { get; init; }
    public int? TrackCount { get; init; }
    public int? DiscNumber { get; init; }
    public int? DiscCount { get; init; }
    public int? Year { get; init; }
    public int? BitrateKbps { get; init; }
    public int? SampleRateHz { get; init; }
    public int? DurationMs { get; init; }
    public long? SizeBytes { get; init; }

    /// <summary>
    /// Path relative to the iPod root with local separators, normalised from
    /// the database's colon-separated form (":iPod_Control:Music:F00:ABCD.mp3").
    /// </summary>
    public string? RelativePath { get; init; }
}

/// <summary>A playlist recovered from the iPod database, with resolved member paths.</summary>
public sealed record IpodPlaylistRecord
{
    public required string Name { get; init; }
    public bool IsMaster { get; init; }
    public IReadOnlyList<string> TrackRelativePaths { get; init; } = Array.Empty<string>();
}
