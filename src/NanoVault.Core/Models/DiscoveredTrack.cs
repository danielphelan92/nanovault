namespace NanoVault.Core.Models;

/// <summary>
/// One audio file found on the iPod. The iPod is never modified; this only
/// describes what was read.
/// </summary>
public sealed record DiscoveredTrack
{
    /// <summary>Absolute path of the file on the mounted iPod volume.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Path relative to the iPod root, using the local separator.</summary>
    public required string RelativeSourcePath { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Lower-case extension including the dot, for example ".mp3".</summary>
    public required string Extension { get; init; }

    public TrackReadStatus Status { get; init; } = TrackReadStatus.Readable;

    public TrackMetadata Metadata { get; init; } = TrackMetadata.Empty;

    public DateTime CreatedUtc { get; init; }
    public DateTime ModifiedUtc { get; init; }

    /// <summary>Human friendly one-line description used across the UI.</summary>
    public string DisplayName
    {
        get
        {
            if (Metadata.HasTitle)
            {
                var artist = Metadata.EffectiveArtist;
                return string.IsNullOrWhiteSpace(artist) ? Metadata.Title! : $"{artist} – {Metadata.Title}";
            }

            return Path.GetFileName(SourcePath);
        }
    }
}
