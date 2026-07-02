using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Playlists;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class M3u8PlaylistWriterTests : IDisposable
{
    private readonly TempWorkspace _destination = new();
    private readonly M3u8PlaylistWriter _writer =
        new(new PhysicalFileSystem(), NullLogger<M3u8PlaylistWriter>.Instance);

    public void Dispose() => _destination.Dispose();

    private static TrackBackupResult Copied(
        string sourceRelative,
        string finalRelative,
        string? title = null,
        string? artist = null,
        int? seconds = null) => new()
    {
        Track = new DiscoveredTrack
        {
            SourcePath = "/ipod/" + sourceRelative,
            RelativeSourcePath = sourceRelative,
            SizeBytes = 10,
            Extension = Path.GetExtension(sourceRelative),
            Metadata = new TrackMetadata
            {
                Title = title,
                Artist = artist,
                Duration = seconds is { } s ? TimeSpan.FromSeconds(s) : null,
            },
        },
        Outcome = TrackOutcome.Copied,
        FinalRelativePath = finalRelative,
    };

    [Fact]
    public async Task Master_playlist_uses_relative_paths_and_extinf()
    {
        var results = new[]
        {
            Copied("iPod_Control/Music/F00/A.mp3", Path.Combine("Artist", "Album", "01 - One.mp3"), "One", "Artist", 61),
            Copied("iPod_Control/Music/F00/B.mp3", Path.Combine("Artist", "Album", "02 - Two.mp3"), "Two", "Artist", 45),
        };

        var relative = await _writer.WriteMasterPlaylistAsync(_destination.Root, results);

        Assert.Equal(M3u8PlaylistWriter.MasterPlaylistName, relative);
        var lines = File.ReadAllLines(Path.Combine(_destination.Root, relative!));

        Assert.Equal("#EXTM3U", lines[0].TrimStart('﻿'));
        Assert.Equal("#EXTINF:61,Artist – One", lines[1]);
        Assert.Equal(Path.Combine("Artist", "Album", "01 - One.mp3"), lines[2]);
        Assert.Equal("#EXTINF:45,Artist – Two", lines[3]);

        // Every path entry is relative — the folder can be moved anywhere.
        Assert.All(lines.Where(l => !l.StartsWith('#') && l.Length > 0), l => Assert.False(Path.IsPathRooted(l)));
    }

    [Fact]
    public async Task Failed_and_not_attempted_tracks_are_excluded()
    {
        var results = new[]
        {
            Copied("a.mp3", "a.mp3", "A"),
            Copied("b.mp3", "b.mp3", "B") with { Outcome = TrackOutcome.Failed },
            Copied("c.mp3", "c.mp3", "C") with { Outcome = TrackOutcome.NotAttempted },
            Copied("d.mp3", "d.mp3", "D") with { Outcome = TrackOutcome.SkippedDuplicate }, // exists → included
        };

        await _writer.WriteMasterPlaylistAsync(_destination.Root, results);

        var content = File.ReadAllText(Path.Combine(_destination.Root, M3u8PlaylistWriter.MasterPlaylistName));
        Assert.Contains("a.mp3", content);
        Assert.DoesNotContain("b.mp3", content);
        Assert.DoesNotContain("c.mp3", content);
        Assert.Contains("d.mp3", content);
    }

    [Fact]
    public async Task No_exported_tracks_means_no_playlist_file()
    {
        var result = await _writer.WriteMasterPlaylistAsync(_destination.Root, Array.Empty<TrackBackupResult>());
        Assert.Null(result);
        Assert.False(File.Exists(Path.Combine(_destination.Root, M3u8PlaylistWriter.MasterPlaylistName)));
    }

    [Fact]
    public async Task Ipod_playlists_are_written_under_playlists_with_parent_relative_paths()
    {
        var results = new[]
        {
            Copied("iPod_Control/Music/F00/A.mp3", Path.Combine("Artist", "Album", "01 - One.mp3"), "One"),
            Copied("iPod_Control/Music/F01/B.mp3", Path.Combine("Artist", "Album", "02 - Two.mp3"), "Two"),
        };

        var playlists = new[]
        {
            new IpodPlaylist
            {
                Name = "Road: Trip?",
                TrackRelativePaths =
                [
                    "iPod_Control/Music/F01/B.mp3",
                    "iPod_Control/Music/F00/MISSING.mp3", // skipped cleanly
                    "iPod_Control/Music/F00/A.mp3",
                ],
            },
        };

        var written = await _writer.WriteIpodPlaylistsAsync(_destination.Root, playlists, results);

        var path = Assert.Single(written);
        Assert.StartsWith("Playlists", path);
        Assert.DoesNotContain("?", path);
        Assert.DoesNotContain(":", Path.GetFileName(path));

        var lines = File.ReadAllLines(Path.Combine(_destination.Root, path));
        var entries = lines.Where(l => !l.StartsWith('#') && l.Length > 0).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.StartsWith("..", e));
        Assert.Contains(entries, e => e.EndsWith("02 - Two.mp3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unmatched_playlists_are_skipped_entirely()
    {
        var playlists = new[]
        {
            new IpodPlaylist { Name = "Empty", TrackRelativePaths = ["nowhere.mp3"] },
        };

        var written = await _writer.WriteIpodPlaylistsAsync(
            _destination.Root, playlists, Array.Empty<TrackBackupResult>());

        Assert.Empty(written);
    }
}
