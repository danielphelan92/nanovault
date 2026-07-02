using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;
using NanoVault.Infrastructure.Backup;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Verification;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class TrackCopyServiceTests : IDisposable
{
    private readonly TempWorkspace _source = new();
    private readonly TempWorkspace _destination = new();
    private readonly TrackCopyService _service;

    public TrackCopyServiceTests()
    {
        var fs = new PhysicalFileSystem();
        _service = new TrackCopyService(fs, new Sha256FileVerificationService(fs), NullLogger<TrackCopyService>.Instance);
    }

    public void Dispose()
    {
        _source.Dispose();
        _destination.Dispose();
    }

    private BackupPlanItem Item(string content, string destinationRelative, PlanItemAction action = PlanItemAction.Copy)
    {
        var path = _source.WriteFile($"F00/{Guid.NewGuid():N}.mp3", content);
        return new BackupPlanItem
        {
            Track = Tracks.FromFile(_source.Root, path),
            DestinationRelativePath = destinationRelative,
            Action = action,
        };
    }

    private static CopyContext Context(bool verify = true, bool preserveTimestamps = true) => new()
    {
        Options = new BackupOptions { VerifyCopies = verify, PreserveTimestamps = preserveTimestamps },
    };

    [Fact]
    public async Task Copies_bytes_exactly_verifies_and_removes_partial()
    {
        var item = Item("the audio bytes", Path.Combine("Artist", "Album", "01 - Song.mp3"));

        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.Copied, result.Outcome);
        Assert.True(result.Verified);
        Assert.NotNull(result.Sha256);

        var destination = Path.Combine(_destination.Root, item.DestinationRelativePath!);
        Assert.Equal("the audio bytes", File.ReadAllText(destination));
        Assert.False(File.Exists(destination + TrackCopyService.PartialSuffix));
    }

    [Fact]
    public async Task Preserves_source_timestamps_when_enabled()
    {
        var item = Item("bytes", "song.mp3");
        var expected = new DateTime(2008, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(item.Track.SourcePath, expected);
        item = item with { Track = Tracks.FromFile(_source.Root, item.Track.SourcePath) };

        await _service.CopyTrackAsync(item, _destination.Root, Context());

        var destination = Path.Combine(_destination.Root, "song.mp3");
        Assert.Equal(expected, File.GetLastWriteTimeUtc(destination));
    }

    [Fact]
    public async Task Skip_items_are_reported_without_touching_disk()
    {
        var item = Item("bytes", "song.mp3", PlanItemAction.SkipExactDuplicate);
        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.SkippedDuplicate, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_destination.Root, "song.mp3")));
    }

    [Fact]
    public async Task Resume_skips_files_already_completed_by_an_earlier_run()
    {
        var item = Item("finished earlier", "song.mp3");
        _destination.WriteFile("song.mp3", "finished earlier");

        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.SkippedDuplicate, result.Outcome);
        Assert.True(result.Verified);
    }

    [Fact]
    public async Task Existing_different_file_is_never_overwritten()
    {
        var item = Item("new bytes", "song.mp3");
        _destination.WriteFile("song.mp3", "precious existing file");

        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.Copied, result.Outcome);
        Assert.Equal("song (2).mp3", result.FinalRelativePath);
        Assert.Equal("precious existing file", File.ReadAllText(Path.Combine(_destination.Root, "song.mp3")));
        Assert.Equal("new bytes", File.ReadAllText(Path.Combine(_destination.Root, "song (2).mp3")));
    }

    [Fact]
    public async Task Replace_action_overwrites_only_when_planned()
    {
        var item = Item("replacement", "song.mp3", PlanItemAction.ReplaceExisting);
        _destination.WriteFile("song.mp3", "old");

        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.Copied, result.Outcome);
        Assert.Equal("replacement", File.ReadAllText(Path.Combine(_destination.Root, "song.mp3")));
    }

    [Fact]
    public async Task Missing_source_fails_cleanly_without_partial_left_behind()
    {
        var item = Item("bytes", "song.mp3");
        File.Delete(item.Track.SourcePath);

        var result = await _service.CopyTrackAsync(item, _destination.Root, Context());

        Assert.Equal(TrackOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(Path.Combine(_destination.Root, "song.mp3")));
        Assert.False(File.Exists(Path.Combine(_destination.Root, "song.mp3" + TrackCopyService.PartialSuffix)));
    }

    [Fact]
    public async Task Cancellation_cleans_up_the_partial_file()
    {
        var big = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(big);
        var path = _source.WriteFile("F00/big.mp3", big);
        var item = new BackupPlanItem
        {
            Track = Tracks.FromFile(_source.Root, path),
            DestinationRelativePath = "big.mp3",
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CopyTrackAsync(item, _destination.Root, Context(), cts.Token));

        Assert.False(File.Exists(Path.Combine(_destination.Root, "big.mp3")));
        Assert.False(File.Exists(Path.Combine(_destination.Root, "big.mp3" + TrackCopyService.PartialSuffix)));
    }

    [Fact]
    public async Task Pause_token_blocks_progress_until_resumed()
    {
        var big = new byte[4 * 1024 * 1024];
        var path = _source.WriteFile("F00/pause.mp3", big);
        var item = new BackupPlanItem
        {
            Track = Tracks.FromFile(_source.Root, path),
            DestinationRelativePath = "pause.mp3",
        };

        var pauseSource = new PauseTokenSource();
        pauseSource.Pause();

        var context = new CopyContext
        {
            Options = new BackupOptions { CopyBufferBytes = 64 * 1024 },
            Pause = pauseSource.Token,
        };

        var copyTask = _service.CopyTrackAsync(item, _destination.Root, context);
        var completedWhilePaused = await Task.WhenAny(copyTask, Task.Delay(300)) == copyTask;
        Assert.False(completedWhilePaused);

        pauseSource.Resume();
        var result = await copyTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(TrackOutcome.Copied, result.Outcome);
    }

    [Fact]
    public async Task Progress_reports_total_bytes_copied()
    {
        var data = new byte[300_000];
        var path = _source.WriteFile("F00/progress.mp3", data);
        var item = new BackupPlanItem
        {
            Track = Tracks.FromFile(_source.Root, path),
            DestinationRelativePath = "progress.mp3",
        };

        long reported = 0;
        var context = new CopyContext
        {
            Options = new BackupOptions { CopyBufferBytes = 64 * 1024 },
            BytesProgress = new TestProgress(delta => Interlocked.Add(ref reported, delta)),
        };

        await _service.CopyTrackAsync(item, _destination.Root, context);
        Assert.Equal(data.Length, reported);
    }

    private sealed class TestProgress : IProgress<long>
    {
        private readonly Action<long> _handler;

        public TestProgress(Action<long> handler) => _handler = handler;

        public void Report(long value) => _handler(value);
    }
}
