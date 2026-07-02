using NanoVault.Core.Models;
using NanoVault.Infrastructure.Playlists;
using NanoVault.TestSupport;
using Xunit;

namespace NanoVault.IntegrationTests;

public class FullBackupWorkflowTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly string _destination;
    private readonly BackupStack _stack = new();

    public FullBackupWorkflowTests()
    {
        _destination = Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
    }

    public void Dispose()
    {
        _ipod.Dispose();
        try
        {
            Directory.Delete(_destination, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void PopulateTypicalIpod()
    {
        var one = _ipod.MusicFile("F00", "AAAA.mp3");
        AudioFixtures.CreateMp3(one);
        AudioFixtures.Tag(one, title: "First Song", artist: "The Band", album: "Debut", track: 1, year: 2004);

        var two = _ipod.MusicFile("F00", "BBBB.mp3");
        AudioFixtures.CreateMp3(two);
        AudioFixtures.Tag(two, title: "Second Song", artist: "The Band", album: "Debut", track: 2, year: 2004);

        var untagged = _ipod.MusicFile("F01", "CCCC.mp3");
        AudioFixtures.CreateMp3(untagged); // metadata comes from the database

        var orphan = _ipod.MusicFile("F01", "DDDD.wav");
        AudioFixtures.CreateWav(orphan); // no tags, no database entry → fallback naming

        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(3, "Database Song", "DB Artist", "DB Album",
                ":iPod_Control:Music:F01:CCCC.mp3", TrackNumber: 5))
            .AddPlaylist("Favourites", isMaster: false, 3));
    }

    [Fact]
    public async Task Complete_backup_produces_organised_verified_output()
    {
        PopulateTypicalIpod();

        var (scan, plan, result) = await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);

        // Scan recovered every layer of metadata.
        Assert.Equal(4, scan.Tracks.Count);

        // All four copied, verified, organised.
        Assert.Equal(BackupState.Completed, result.FinalState);
        Assert.Equal(4, result.CopiedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.All(result.TrackResults, r => Assert.True(r.Verified));

        Assert.True(File.Exists(Path.Combine(_destination, "The Band", "Debut", "01 - First Song.mp3")));
        Assert.True(File.Exists(Path.Combine(_destination, "The Band", "Debut", "02 - Second Song.mp3")));
        Assert.True(File.Exists(Path.Combine(_destination, "DB Artist", "DB Album", "05 - Database Song.mp3")));
        Assert.True(File.Exists(Path.Combine(_destination, "Unknown Artist", "Unknown Album", "Unknown Track - DDDD.wav")));

        // Copied bytes are identical to the source (no transcoding).
        var sourceBytes = File.ReadAllBytes(_ipod.MusicFile("F00", "AAAA.mp3"));
        var destinationBytes = File.ReadAllBytes(Path.Combine(_destination, "The Band", "Debut", "01 - First Song.mp3"));
        Assert.Equal(sourceBytes, destinationBytes);

        // Master playlist and the recovered iPod playlist exist with relative paths.
        var master = Path.Combine(_destination, M3u8PlaylistWriter.MasterPlaylistName);
        Assert.True(File.Exists(master));
        var masterEntries = File.ReadAllLines(master).Where(l => !l.StartsWith('#') && l.Length > 0).ToList();
        Assert.Equal(4, masterEntries.Count);
        Assert.All(masterEntries, e => Assert.False(Path.IsPathRooted(e)));

        Assert.True(File.Exists(Path.Combine(_destination, "Playlists", "Favourites.m3u8")));

        // Reports were generated.
        Assert.NotNull(result.HtmlReportPath);
        Assert.NotNull(result.JsonReportPath);
        Assert.True(File.Exists(result.HtmlReportPath!));

        // No partial files remain anywhere.
        Assert.Empty(Directory.EnumerateFiles(_destination, "*.nanovault-partial", SearchOption.AllDirectories));

        // The iPod itself is untouched: same files, same content.
        Assert.Equal(4 + 1 /* iTunesDB */, Directory.EnumerateFiles(_ipod.Root, "*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task Second_backup_skips_every_exact_duplicate()
    {
        PopulateTypicalIpod();

        await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);
        var (_, plan, result) = await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);

        Assert.Equal(4, plan.SkippedDuplicateCount);
        Assert.Equal(0, result.CopiedCount);
        Assert.Equal(4, result.SkippedDuplicateCount);

        // Still exactly one copy of each track on disk.
        var audioFiles = Directory.EnumerateFiles(_destination, "*.mp3", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(_destination, "*.wav", SearchOption.AllDirectories));
        Assert.Equal(4, audioFiles.Count());
    }

    [Fact]
    public async Task Corrupt_file_still_copies_by_bytes_with_fallback_name()
    {
        AudioFixtures.CreateCorrupt(_ipod.MusicFile("F00", "JUNK.mp3"));

        var (_, _, result) = await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);

        Assert.Equal(1, result.CopiedCount);
        var copied = Path.Combine(_destination, "Unknown Artist", "Unknown Album", "Unknown Track - JUNK.mp3");
        Assert.True(File.Exists(copied));
        Assert.Equal(File.ReadAllBytes(_ipod.MusicFile("F00", "JUNK.mp3")), File.ReadAllBytes(copied));
    }

    [Fact]
    public async Task Protected_file_is_copied_as_is_and_labelled()
    {
        AudioFixtures.CreateCorrupt(_ipod.MusicFile("F00", "DRMM.m4p"));

        var (scan, plan, result) = await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);

        Assert.Equal(1, scan.ProtectedCount);
        Assert.Equal(1, plan.ProtectedCopyCount);
        Assert.Equal(1, result.CopiedCount);
        Assert.Single(Directory.EnumerateFiles(_destination, "*.m4p", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancellation_keeps_completed_files_and_resume_finishes_the_rest()
    {
        // Enough tracks that cancelling after the first leaves work undone.
        for (var i = 0; i < 12; i++)
        {
            var file = _ipod.MusicFile("F00", $"SNG{i:00}.mp3");
            AudioFixtures.CreateMp3(file, frames: 200);
            AudioFixtures.Tag(file, title: $"Song {i:00}", artist: "Artist", album: "Album", track: (uint)(i + 1));
        }

        var scan = await _stack.Scanner.ScanAsync(_ipod.ToDevice());
        var plan = await _stack.Planner.CreatePlanAsync(scan, scan.Tracks, _destination, new BackupOptions());

        using var cts = new CancellationTokenSource();
        var copied = 0;
        var progress = new SynchronousProgress(p =>
        {
            if (p.CompletedTracks >= 3 && copied == 0)
            {
                copied = p.CompletedTracks;
                cts.Cancel();
            }
        });

        var result = await _stack.Service.RunAsync(plan, progress, default, cts.Token);

        Assert.Equal(BackupState.Cancelled, result.FinalState);
        Assert.True(result.CopiedCount >= 3);
        Assert.True(result.NotAttemptedCount > 0);
        Assert.Empty(Directory.EnumerateFiles(_destination, "*.nanovault-partial", SearchOption.AllDirectories));

        // Resume: run again — already-copied tracks are skipped, the rest copied.
        var (_, resumePlan, resumeResult) = await _stack.BackupEverythingAsync(_ipod.ToDevice(), _destination);

        Assert.Equal(BackupState.Completed, resumeResult.FinalState);
        Assert.Equal(12, resumeResult.CopiedCount + resumeResult.SkippedDuplicateCount);
        Assert.Equal(result.CopiedCount, resumePlan.SkippedDuplicateCount);

        var files = Directory.EnumerateFiles(_destination, "*.mp3", SearchOption.AllDirectories).ToList();
        Assert.Equal(12, files.Count);
    }

    [Fact]
    public async Task Failed_track_does_not_stop_the_others()
    {
        var good = _ipod.MusicFile("F00", "GOOD.mp3");
        AudioFixtures.CreateMp3(good);
        AudioFixtures.Tag(good, title: "Good", artist: "A", album: "B");

        var ghost = _ipod.MusicFile("F00", "GONE.mp3");
        AudioFixtures.CreateMp3(ghost);

        var scan = await _stack.Scanner.ScanAsync(_ipod.ToDevice());
        var plan = await _stack.Planner.CreatePlanAsync(scan, scan.Tracks, _destination, new BackupOptions());

        // The file disappears between planning and copying.
        File.Delete(ghost);

        var result = await _stack.Service.RunAsync(plan);

        Assert.Equal(BackupState.Completed, result.FinalState);
        Assert.Equal(1, result.CopiedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Warnings, w => w.Severity == WarningSeverity.Error);
    }

    private sealed class SynchronousProgress : IProgress<BackupProgress>
    {
        private readonly Action<BackupProgress> _handler;

        public SynchronousProgress(Action<BackupProgress> handler) => _handler = handler;

        public void Report(BackupProgress value) => _handler(value);
    }
}
