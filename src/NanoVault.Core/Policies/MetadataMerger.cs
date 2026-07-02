using NanoVault.Core.Models;

namespace NanoVault.Core.Policies;

/// <summary>
/// Merges the metadata layers with a fixed precedence:
/// embedded tags first, then the iPod database, then fallback naming.
/// </summary>
public static class MetadataMerger
{
    public static TrackMetadata Merge(TrackMetadata? embeddedTags, IpodTrackRecord? databaseRecord)
    {
        var tags = embeddedTags ?? TrackMetadata.Empty;
        var db = databaseRecord;

        var usedTags = false;
        var usedDb = false;

        string? PickText(string? tagValue, string? dbValue)
        {
            if (!string.IsNullOrWhiteSpace(tagValue))
            {
                usedTags = true;
                return tagValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dbValue))
            {
                usedDb = true;
                return dbValue.Trim();
            }

            return null;
        }

        int? PickNumber(int? tagValue, int? dbValue)
        {
            if (tagValue is > 0)
            {
                usedTags = true;
                return tagValue;
            }

            if (dbValue is > 0)
            {
                usedDb = true;
                return dbValue;
            }

            return null;
        }

        TimeSpan? duration;
        if (tags.Duration is { } tagDuration && tagDuration > TimeSpan.Zero)
        {
            usedTags = true;
            duration = tagDuration;
        }
        else if (db?.DurationMs is int ms and > 0)
        {
            usedDb = true;
            duration = TimeSpan.FromMilliseconds(ms);
        }
        else
        {
            duration = null;
        }

        var merged = new TrackMetadata
        {
            Title = PickText(tags.Title, db?.Title),
            Artist = PickText(tags.Artist, db?.Artist),
            AlbumArtist = PickText(tags.AlbumArtist, db?.AlbumArtist),
            Album = PickText(tags.Album, db?.Album),
            Genre = PickText(tags.Genre, db?.Genre),
            Composer = PickText(tags.Composer, db?.Composer),
            TrackNumber = PickNumber(tags.TrackNumber, db?.TrackNumber),
            TrackCount = PickNumber(tags.TrackCount, db?.TrackCount),
            DiscNumber = PickNumber(tags.DiscNumber, db?.DiscNumber),
            DiscCount = PickNumber(tags.DiscCount, db?.DiscCount),
            Year = PickNumber(tags.Year, db?.Year),
            BitrateKbps = PickNumber(tags.BitrateKbps, db?.BitrateKbps),
            SampleRateHz = PickNumber(tags.SampleRateHz, db?.SampleRateHz),
            Duration = duration,
            Format = tags.Format,
            HasArtwork = tags.HasArtwork,
        };

        var sources = MetadataSources.None;
        if (usedTags)
        {
            sources |= MetadataSources.EmbeddedTags;
        }

        if (usedDb)
        {
            sources |= MetadataSources.IpodDatabase;
        }

        if (!merged.HasTitle)
        {
            sources |= MetadataSources.FallbackNaming;
        }

        return merged with { Sources = sources };
    }
}
