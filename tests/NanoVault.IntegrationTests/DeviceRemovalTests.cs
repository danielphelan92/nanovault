using NanoVault.Core.Models;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.TestSupport;
using Xunit;

namespace NanoVault.IntegrationTests;

public class DeviceRemovalTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly string _destination;

    public DeviceRemovalTests()
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

    [Fact]
    public async Task Unplugging_mid_backup_yields_a_recoverable_interrupted_state()
    {
        for (var i = 0; i < 10; i++)
        {
            var file = _ipod.MusicFile("F00", $"TRK{i:00}.mp3");
            AudioFixtures.CreateMp3(file, frames: 400);
            AudioFixtures.Tag(file, title: $"Track {i:00}", artist: "Artist", album: "Album", track: (uint)(i + 1));
        }

        // Plan against the healthy device, then "unplug" it after ~40% of the
        // total bytes have been read.
        var healthyStack = new BackupStack();
        var device = _ipod.ToDevice();
        var scan = await healthyStack.Scanner.ScanAsync(device);
        var plan = await healthyStack.Planner.CreatePlanAsync(scan, scan.Tracks, _destination, new BackupOptions());

        var removable = new RemovableFileSystem(
            new PhysicalFileSystem(), _ipod.Root, removeAfterBytes: plan.BytesToCopy * 2 / 5);
        var failingStack = new BackupStack(removable);

        var result = await failingStack.Service.RunAsync(plan);

        Assert.Equal(BackupState.Interrupted, result.FinalState);
        Assert.True(result.CopiedCount is > 0 and < 10, $"copied {result.CopiedCount}");
        Assert.True(result.FailedCount >= 1);
        Assert.True(result.NotAttemptedCount > 0);

        // Completed files are intact and verified; no partial files remain.
        foreach (var copied in result.TrackResults.Where(r => r.Outcome == TrackOutcome.Copied))
        {
            var path = Path.Combine(_destination, copied.FinalRelativePath!);
            Assert.True(File.Exists(path));
            Assert.Equal(copied.Track.SizeBytes, new FileInfo(path).Length);
        }

        Assert.Empty(Directory.EnumerateFiles(_destination, "*.nanovault-partial", SearchOption.AllDirectories));

        // The report still tells the user exactly what happened.
        Assert.NotNull(result.HtmlReportPath);

        // Reconnect (healthy filesystem again) and resume: everything completes.
        var (_, _, resume) = await healthyStack.BackupEverythingAsync(device, _destination);
        Assert.Equal(BackupState.Completed, resume.FinalState);
        Assert.Equal(10, resume.CopiedCount + resume.SkippedDuplicateCount);
    }

    [Fact]
    public async Task Device_vanishing_before_scan_produces_error_warning_not_crash()
    {
        AudioFixtures.CreateMp3(_ipod.MusicFile("F00", "SONG.mp3"));

        var removable = new RemovableFileSystem(new PhysicalFileSystem(), _ipod.Root);
        removable.RemoveDevice();
        var stack = new BackupStack(removable);

        var scan = await stack.Scanner.ScanAsync(_ipod.ToDevice());

        Assert.Empty(scan.Tracks);
        Assert.Contains(scan.Warnings, w => w.Severity == WarningSeverity.Error);
    }
}
