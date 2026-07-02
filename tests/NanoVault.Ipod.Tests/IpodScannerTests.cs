using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Metadata;
using NanoVault.Ipod.Database;
using NanoVault.TestSupport;
using Xunit;

namespace NanoVault.Ipod.Tests;

public class IpodScannerTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly IpodScanner _scanner;

    public IpodScannerTests()
    {
        var fs = new PhysicalFileSystem();
        _scanner = new IpodScanner(
            fs,
            new TagLibAudioMetadataReader(fs, NullLogger<TagLibAudioMetadataReader>.Instance),
            new ITunesDbReader(fs, NullLogger<ITunesDbReader>.Instance),
            NullLogger<IpodScanner>.Instance);
    }

    public void Dispose() => _ipod.Dispose();

    [Fact]
    public async Task Scan_finds_supported_files_and_reads_embedded_tags()
    {
        var mp3 = _ipod.MusicFile("F00", "ABCD.mp3");
        AudioFixtures.CreateMp3(mp3);
        AudioFixtures.Tag(mp3, title: "Tagged Song", artist: "Tag Artist", album: "Tag Album", track: 3);

        var wav = _ipod.MusicFile("F01", "EFGH.wav");
        AudioFixtures.CreateWav(wav);

        File.WriteAllText(_ipod.MusicFile("F00", "notes.txt"), "not audio"); // ignored

        var scan = await _scanner.ScanAsync(_ipod.ToDevice());

        Assert.Equal(2, scan.Tracks.Count);
        var tagged = scan.Tracks.Single(t => t.Extension == ".mp3");
        Assert.Equal("Tagged Song", tagged.Metadata.Title);
        Assert.Equal("Tag Artist", tagged.Metadata.Artist);
        Assert.Equal(3, tagged.Metadata.TrackNumber);
        Assert.True(tagged.Metadata.Sources.HasFlag(MetadataSources.EmbeddedTags));
    }

    [Fact]
    public async Task Database_fills_in_when_tags_are_missing()
    {
        var mp3 = _ipod.MusicFile("F00", "NOTG.mp3");
        AudioFixtures.CreateMp3(mp3); // no tags at all

        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(
                7, "From Database", "DB Artist", "DB Album",
                ":iPod_Control:Music:F00:NOTG.mp3", TrackNumber: 9)));

        var scan = await _scanner.ScanAsync(_ipod.ToDevice());

        var track = Assert.Single(scan.Tracks);
        Assert.Equal("From Database", track.Metadata.Title);
        Assert.Equal("DB Artist", track.Metadata.Artist);
        Assert.Equal(9, track.Metadata.TrackNumber);
        Assert.True(track.Metadata.Sources.HasFlag(MetadataSources.IpodDatabase));
    }

    [Fact]
    public async Task Corrupt_audio_still_included_with_fallback_naming()
    {
        AudioFixtures.CreateCorrupt(_ipod.MusicFile("F00", "BROK.mp3"));

        var scan = await _scanner.ScanAsync(_ipod.ToDevice());

        var track = Assert.Single(scan.Tracks);
        Assert.Equal(TrackReadStatus.Readable, track.Status); // bytes are readable
        Assert.False(track.Metadata.HasTitle);
        Assert.True(track.Metadata.Sources.HasFlag(MetadataSources.FallbackNaming));
    }

    [Fact]
    public async Task Protected_extension_is_classified_not_decrypted()
    {
        AudioFixtures.CreateCorrupt(_ipod.MusicFile("F00", "PROT.m4p"));

        var scan = await _scanner.ScanAsync(_ipod.ToDevice());

        var track = Assert.Single(scan.Tracks);
        Assert.Equal(TrackReadStatus.Protected, track.Status);
    }

    [Fact]
    public async Task Playlists_map_to_discovered_tracks_only()
    {
        var mp3 = _ipod.MusicFile("F00", "LIST.mp3");
        AudioFixtures.CreateMp3(mp3);

        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "On Disk", "A", "B", ":iPod_Control:Music:F00:LIST.mp3"))
            .AddTrack(new ITunesDbBuilder.TrackSpec(2, "Ghost", "A", "B", ":iPod_Control:Music:F00:GONE.mp3"))
            .AddPlaylist("Master", isMaster: true, 1, 2)
            .AddPlaylist("Mine", isMaster: false, 1, 2));

        var scan = await _scanner.ScanAsync(_ipod.ToDevice());

        var playlist = Assert.Single(scan.Playlists); // master excluded
        Assert.Equal("Mine", playlist.Name);
        var member = Assert.Single(playlist.TrackRelativePaths); // ghost file excluded
        Assert.EndsWith("LIST.mp3", member);
    }

    [Fact]
    public async Task Missing_music_folder_reports_error_warning()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var device = _ipod.ToDevice() with { RootPath = emptyRoot };
            var scan = await _scanner.ScanAsync(device);

            Assert.Empty(scan.Tracks);
            Assert.Contains(scan.Warnings, w => w.Severity == WarningSeverity.Error);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_reports_progress_and_honours_cancellation()
    {
        for (var i = 0; i < 10; i++)
        {
            AudioFixtures.CreateMp3(_ipod.MusicFile("F00", $"T{i:00}.mp3"));
        }

        using var cts = new CancellationTokenSource();
        var seen = 0;
        var progress = new Progress<ScanProgress>(_ => Interlocked.Increment(ref seen));

        // Cancel immediately: the scan must observe it and throw.
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _scanner.ScanAsync(_ipod.ToDevice(), progress, cts.Token));
    }
}
