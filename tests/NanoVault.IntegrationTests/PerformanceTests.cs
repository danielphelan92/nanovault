using System.Diagnostics;
using NanoVault.Core.Models;
using NanoVault.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace NanoVault.IntegrationTests;

/// <summary>
/// Performance guard: NanoVault must stay responsive with a 10,000-track
/// library (spec phase 7). Uses database-only metadata so fixture generation
/// stays fast; the point is scanner/planner throughput at scale.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly string _destination;
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
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
    public async Task Ten_thousand_tracks_scan_and_plan_in_reasonable_time()
    {
        const int trackCount = 10_000;

        // One real MP3 duplicated 10,000 times across F00–F49.
        var template = Path.Combine(Path.GetTempPath(), "nanovault-tests", $"template-{Guid.NewGuid():N}.mp3");
        AudioFixtures.CreateMp3(template, frames: 4);
        var templateBytes = File.ReadAllBytes(template);

        var databaseBuilder = new ITunesDbBuilder();
        for (var i = 0; i < trackCount; i++)
        {
            var folder = $"F{i % 50:00}";
            var name = $"{i:X4}.mp3";
            var directory = Path.Combine(_ipod.MusicRoot, folder);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name), templateBytes);

            databaseBuilder.AddTrack(new ITunesDbBuilder.TrackSpec(
                (uint)(i + 1),
                Title: $"Track {i:00000}",
                Artist: $"Artist {i % 200:000}",
                Album: $"Album {i % 800:000}",
                IpodPath: $":iPod_Control:Music:{folder}:{name}",
                TrackNumber: i % 15 + 1));
        }

        _ipod.WriteDatabase(databaseBuilder);
        File.Delete(template);

        var stack = new BackupStack();
        var stopwatch = Stopwatch.StartNew();

        var scan = await stack.Scanner.ScanAsync(_ipod.ToDevice());
        var scanTime = stopwatch.Elapsed;

        Assert.Equal(trackCount, scan.Tracks.Count);
        Assert.All(scan.Tracks.Take(100), t => Assert.True(t.Metadata.HasTitle));

        stopwatch.Restart();
        var plan = await stack.Planner.CreatePlanAsync(scan, scan.Tracks, _destination, new BackupOptions());
        var planTime = stopwatch.Elapsed;

        Assert.Equal(trackCount, plan.CopyCount);
        Assert.Equal(trackCount, plan.Items.Select(i => i.DestinationRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        _output.WriteLine($"Scan: {scanTime.TotalSeconds:0.0}s, Plan: {planTime.TotalSeconds:0.0}s");

        // Generous ceilings so slow CI never flakes, but regressions are caught.
        Assert.True(scanTime < TimeSpan.FromMinutes(4), $"Scan took {scanTime}");
        Assert.True(planTime < TimeSpan.FromMinutes(2), $"Plan took {planTime}");
    }
}
