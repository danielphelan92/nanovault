using NanoVault.Core.Models;
using Xunit;

namespace NanoVault.Core.Tests;

public class BackupResultTests
{
    private static DiscoveredTrack Track(string name) => new()
    {
        SourcePath = $"/ipod/iPod_Control/Music/F00/{name}",
        RelativeSourcePath = $"iPod_Control/Music/F00/{name}",
        SizeBytes = 100,
        Extension = ".mp3",
    };

    private static TrackBackupResult Result(string name, TrackOutcome outcome, long bytes = 0) => new()
    {
        Track = Track(name),
        Outcome = outcome,
        BytesCopied = bytes,
    };

    [Fact]
    public void Counts_aggregate_by_outcome()
    {
        var startedAt = DateTimeOffset.Now;
        var result = new BackupResult
        {
            DestinationRoot = "/dest",
            DeviceName = "iPod",
            StartedAt = startedAt,
            FinishedAt = startedAt.AddMinutes(5),
            TrackResults =
            [
                Result("a.mp3", TrackOutcome.Copied, 100),
                Result("b.mp3", TrackOutcome.Copied, 100),
                Result("c.mp3", TrackOutcome.SkippedDuplicate),
                Result("d.mp3", TrackOutcome.Failed),
                Result("e.mp3", TrackOutcome.NotAttempted),
            ],
            Warnings =
            [
                new BackupWarning("problem", Severity: WarningSeverity.Warning),
                new BackupWarning("info only", Severity: WarningSeverity.Info),
            ],
        };

        Assert.Equal(2, result.CopiedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.NotAttemptedCount);
        Assert.Equal(200, result.TotalCopiedBytes);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Elapsed);
    }

    [Fact]
    public void Final_states_map_to_flags()
    {
        var basis = new BackupResult { DestinationRoot = "d", DeviceName = "n" };
        Assert.True((basis with { FinalState = BackupState.Cancelled }).WasCancelled);
        Assert.True((basis with { FinalState = BackupState.Interrupted }).WasInterrupted);
        Assert.False((basis with { FinalState = BackupState.Completed }).WasCancelled);
    }

    [Fact]
    public void Progress_percent_uses_bytes_and_clamps()
    {
        var progress = new BackupProgress { TotalBytes = 200, CopiedBytes = 50 };
        Assert.Equal(25, progress.Percent);

        var over = new BackupProgress { TotalBytes = 100, CopiedBytes = 150 };
        Assert.Equal(100, over.Percent);

        var empty = new BackupProgress();
        Assert.Equal(0, empty.Percent);
    }

    [Fact]
    public void Estimated_remaining_needs_measured_speed()
    {
        var idle = new BackupProgress { TotalBytes = 100, CopiedBytes = 0, BytesPerSecond = 0 };
        Assert.Null(idle.EstimatedRemaining);

        var moving = new BackupProgress { TotalBytes = 1000, CopiedBytes = 500, BytesPerSecond = 100 };
        Assert.Equal(TimeSpan.FromSeconds(5), moving.EstimatedRemaining);
    }
}
