using NanoVault.Core.Models;
using NanoVault.Core.Policies;
using Xunit;

namespace NanoVault.Core.Tests;

public class OutputPathBuilderTests
{
    private static DiscoveredTrack Track(
        TrackMetadata metadata,
        string sourceName = "ABCD.mp3",
        string extension = ".mp3") => new()
    {
        SourcePath = Path.Combine("E:", "iPod_Control", "Music", "F00", sourceName),
        RelativeSourcePath = Path.Combine("iPod_Control", "Music", "F00", sourceName),
        SizeBytes = 1000,
        Extension = extension,
        Metadata = metadata,
    };

    private static string P(params string[] parts) => string.Join(Path.DirectorySeparatorChar, parts);

    [Fact]
    public void Default_template_is_artist_album_number_title()
    {
        var track = Track(new TrackMetadata
        {
            Title = "Yellow Submarine",
            Artist = "The Beatles",
            Album = "Revolver",
            TrackNumber = 6,
        });

        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.Equal(P("The Beatles", "Revolver", "06 - Yellow Submarine.mp3"), path);
    }

    [Fact]
    public void Album_artist_year_template()
    {
        var track = Track(new TrackMetadata
        {
            Title = "Song",
            Artist = "Feat Artist",
            AlbumArtist = "Main Artist",
            Album = "The Album",
            Year = 1999,
            TrackNumber = 1,
        });

        var options = new BackupOptions { Template = OrganizationTemplate.AlbumArtistYearAlbum };
        var path = OutputPathBuilder.BuildRelativePath(track, options);
        Assert.Equal(P("Main Artist", "1999 - The Album", "01 - Song.mp3"), path);
    }

    [Fact]
    public void Flat_template_is_artist_dash_title()
    {
        var track = Track(new TrackMetadata { Title = "Song", Artist = "Artist", Album = "Album" });
        var options = new BackupOptions { Template = OrganizationTemplate.FlatAllMusic };
        var path = OutputPathBuilder.BuildRelativePath(track, options);
        Assert.Equal(P("All Music", "Artist - Song.mp3"), path);
    }

    [Fact]
    public void Multi_disc_albums_get_disc_subfolders()
    {
        var track = Track(new TrackMetadata
        {
            Title = "Song",
            Artist = "Artist",
            Album = "Big Album",
            TrackNumber = 3,
            DiscNumber = 2,
            DiscCount = 2,
        });

        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.Equal(P("Artist", "Big Album", "Disc 2", "03 - Song.mp3"), path);
    }

    [Fact]
    public void Multi_disc_without_subfolders_prefixes_the_number()
    {
        var track = Track(new TrackMetadata
        {
            Title = "Song",
            Artist = "Artist",
            Album = "Big Album",
            TrackNumber = 3,
            DiscNumber = 2,
            DiscCount = 2,
        });

        var options = new BackupOptions { UseDiscSubfolders = false };
        var path = OutputPathBuilder.BuildRelativePath(track, options);
        Assert.Equal(P("Artist", "Big Album", "2-03 - Song.mp3"), path);
    }

    [Fact]
    public void Missing_metadata_uses_safe_fallback_naming()
    {
        var track = Track(TrackMetadata.Empty, sourceName: "XQZT.mp3");
        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.Equal(P("Unknown Artist", "Unknown Album", "Unknown Track - XQZT.mp3"), path);
    }

    [Fact]
    public void Title_without_artist_uses_unknown_artist_folder()
    {
        var track = Track(new TrackMetadata { Title = "Lonely Song" });
        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.Equal(P("Unknown Artist", "Unknown Album", "Lonely Song.mp3"), path);
    }

    [Fact]
    public void Track_number_missing_omits_the_prefix()
    {
        var track = Track(new TrackMetadata { Title = "Song", Artist = "Artist", Album = "Album" });
        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.EndsWith(P("Album", "Song.mp3"), path);
    }

    [Fact]
    public void Extremely_long_metadata_stays_within_budget()
    {
        var track = Track(new TrackMetadata
        {
            Title = new string('t', 300),
            Artist = new string('a', 300),
            Album = new string('b', 300),
            TrackNumber = 1,
        });

        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.True(path.Length <= OutputPathBuilder.MaxRelativePathLength,
            $"Path length {path.Length}: {path}");
        Assert.EndsWith(".mp3", path);
    }

    [Fact]
    public void Original_extension_is_always_preserved()
    {
        var track = Track(new TrackMetadata { Title = "Book", Artist = "Reader" }, "AUDIO.m4b", ".m4b");
        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        Assert.EndsWith(".m4b", path);
    }

    [Fact]
    public void Invalid_characters_in_metadata_never_reach_the_path()
    {
        var track = Track(new TrackMetadata
        {
            Title = "What? A: Song*",
            Artist = "AC/DC",
            Album = "Back<>In|Black",
            TrackNumber = 1,
        });

        var path = OutputPathBuilder.BuildRelativePath(track, new BackupOptions());
        foreach (var invalid in new[] { '<', '>', ':', '"', '|', '?', '*' })
        {
            Assert.DoesNotContain(invalid, path);
        }
    }
}
