namespace NanoVault.Core.Models;

/// <summary>
/// Merged, display-ready metadata for a single track. Fields are null when no
/// reliable value was recovered from embedded tags or the iPod database.
/// </summary>
public sealed record TrackMetadata
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumArtist { get; init; }
    public string? Album { get; init; }
    public int? TrackNumber { get; init; }
    public int? TrackCount { get; init; }
    public int? DiscNumber { get; init; }
    public int? DiscCount { get; init; }
    public int? Year { get; init; }
    public string? Genre { get; init; }
    public string? Composer { get; init; }
    public TimeSpan? Duration { get; init; }
    public int? BitrateKbps { get; init; }
    public int? SampleRateHz { get; init; }
    public string? Format { get; init; }
    public bool HasArtwork { get; init; }

    /// <summary>Which layers contributed at least one field.</summary>
    public MetadataSources Sources { get; init; } = MetadataSources.None;

    public static TrackMetadata Empty { get; } = new();

    /// <summary>Best value to show a person: artist, else album artist.</summary>
    public string? EffectiveArtist => string.IsNullOrWhiteSpace(Artist) ? AlbumArtist : Artist;

    /// <summary>Best grouping artist: album artist, else artist.</summary>
    public string? EffectiveAlbumArtist => string.IsNullOrWhiteSpace(AlbumArtist) ? Artist : AlbumArtist;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}
