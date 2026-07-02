using NanoVault.Core.Models;
using NanoVault.Core.Policies;
using Xunit;

namespace NanoVault.Core.Tests;

public class MetadataMergerTests
{
    [Fact]
    public void Embedded_tags_win_over_database_values()
    {
        var tags = new TrackMetadata { Title = "Tag Title", Artist = "Tag Artist", TrackNumber = 7 };
        var db = new IpodTrackRecord { Title = "DB Title", Artist = "DB Artist", TrackNumber = 9 };

        var merged = MetadataMerger.Merge(tags, db);

        Assert.Equal("Tag Title", merged.Title);
        Assert.Equal("Tag Artist", merged.Artist);
        Assert.Equal(7, merged.TrackNumber);
        Assert.True(merged.Sources.HasFlag(MetadataSources.EmbeddedTags));
    }

    [Fact]
    public void Database_fills_gaps_left_by_tags()
    {
        var tags = new TrackMetadata { Title = "Tag Title" };
        var db = new IpodTrackRecord { Album = "DB Album", Genre = "Rock", Year = 1984 };

        var merged = MetadataMerger.Merge(tags, db);

        Assert.Equal("Tag Title", merged.Title);
        Assert.Equal("DB Album", merged.Album);
        Assert.Equal("Rock", merged.Genre);
        Assert.Equal(1984, merged.Year);
        Assert.True(merged.Sources.HasFlag(MetadataSources.EmbeddedTags));
        Assert.True(merged.Sources.HasFlag(MetadataSources.IpodDatabase));
    }

    [Fact]
    public void Whitespace_tag_values_do_not_win()
    {
        var tags = new TrackMetadata { Title = "   " };
        var db = new IpodTrackRecord { Title = "DB Title" };

        var merged = MetadataMerger.Merge(tags, db);
        Assert.Equal("DB Title", merged.Title);
    }

    [Fact]
    public void No_title_anywhere_flags_fallback_naming()
    {
        var merged = MetadataMerger.Merge(new TrackMetadata { Artist = "Someone" }, null);
        Assert.True(merged.Sources.HasFlag(MetadataSources.FallbackNaming));
        Assert.False(merged.HasTitle);
    }

    [Fact]
    public void Duration_from_database_when_tags_lack_it()
    {
        var db = new IpodTrackRecord { DurationMs = 90_000 };
        var merged = MetadataMerger.Merge(null, db);
        Assert.Equal(TimeSpan.FromSeconds(90), merged.Duration);
        Assert.True(merged.Sources.HasFlag(MetadataSources.IpodDatabase));
    }

    [Fact]
    public void Null_inputs_produce_empty_metadata()
    {
        var merged = MetadataMerger.Merge(null, null);
        Assert.Null(merged.Title);
        Assert.Null(merged.Artist);
        Assert.True(merged.Sources.HasFlag(MetadataSources.FallbackNaming));
    }

    [Fact]
    public void Effective_artist_falls_back_to_album_artist()
    {
        var merged = MetadataMerger.Merge(new TrackMetadata { Title = "T", AlbumArtist = "AA" }, null);
        Assert.Equal("AA", merged.EffectiveArtist);
    }
}
