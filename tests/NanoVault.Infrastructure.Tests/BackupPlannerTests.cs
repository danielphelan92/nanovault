using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.Backup;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Verification;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class BackupPlannerTests : IDisposable
{
    private readonly TempWorkspace _source = new();
    private readonly TempWorkspace _destination = new();
    private readonly BackupPlanner _planner;

    public BackupPlannerTests()
    {
        var fs = new PhysicalFileSystem();
        var verification = new Sha256FileVerificationService(fs);
        _planner = new BackupPlanner(
            fs,
            new DuplicateResolver(fs, verification, NullLogger<DuplicateResolver>.Instance),
            NullLogger<BackupPlanner>.Instance);
    }

    public void Dispose()
    {
        _source.Dispose();
        _destination.Dispose();
    }

    private DiscoveredTrack Track(string name, string content, TrackMetadata metadata, TrackReadStatus status = TrackReadStatus.Readable)
    {
        var path = _source.WriteFile($"iPod_Control/Music/F00/{name}", content);
        return Tracks.FromFile(_source.Root, path, metadata) with { Status = status };
    }

    private ScanResult Scan(params DiscoveredTrack[] tracks) => new()
    {
        Device = new IpodDevice { RootPath = _source.Root, HasMusicFolder = true },
        Tracks = tracks,
    };

    [Fact]
    public async Task Plan_builds_destinations_from_the_template()
    {
        var track = Track("AAAA.mp3", "content", new TrackMetadata
        {
            Title = "Song",
            Artist = "Artist",
            Album = "Album",
            TrackNumber = 1,
        });

        var plan = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root, new BackupOptions());

        var item = Assert.Single(plan.Items);
        Assert.Equal(PlanItemAction.Copy, item.Action);
        Assert.Equal(Path.Combine("Artist", "Album", "01 - Song.mp3"), item.DestinationRelativePath);
        Assert.Equal(1, plan.CopyCount);
        Assert.Equal(track.SizeBytes, plan.BytesToCopy);
    }

    [Fact]
    public async Task Existing_identical_file_becomes_skip_duplicate()
    {
        var track = Track("AAAA.mp3", "same bytes", new TrackMetadata { Title = "Song", Artist = "A", Album = "B" });
        _destination.WriteFile($"A/B/Song.mp3", "same bytes");

        var plan = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root, new BackupOptions());

        var item = Assert.Single(plan.Items);
        Assert.Equal(PlanItemAction.SkipExactDuplicate, item.Action);
        Assert.Equal(0, plan.CopyCount);
        Assert.Equal(1, plan.SkippedDuplicateCount);
    }

    [Fact]
    public async Task Unreadable_tracks_are_skipped_with_a_note()
    {
        var track = Track("BAD.mp3", "x", TrackMetadata.Empty, TrackReadStatus.Unreadable);
        var plan = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root, new BackupOptions());

        var item = Assert.Single(plan.Items);
        Assert.Equal(PlanItemAction.SkipUnreadable, item.Action);
        Assert.False(item.WillCopy);
    }

    [Fact]
    public async Task Protected_tracks_follow_the_include_setting()
    {
        var track = Track("PROT.m4p", "drm", TrackMetadata.Empty, TrackReadStatus.Protected);

        var included = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root,
            new BackupOptions { IncludeProtectedFiles = true });
        Assert.True(included.Items.Single().WillCopy);
        Assert.Equal(1, included.ProtectedCopyCount);

        var excluded = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root,
            new BackupOptions { IncludeProtectedFiles = false });
        Assert.Equal(PlanItemAction.SkipProtectedExcluded, excluded.Items.Single().Action);
    }

    [Fact]
    public async Task Two_tracks_with_identical_metadata_get_distinct_destinations()
    {
        var metadata = new TrackMetadata { Title = "Same Song", Artist = "Artist", Album = "Album" };
        var first = Track("AAAA.mp3", "first content", metadata);
        var second = Track("BBBB.mp3", "second content", metadata);

        var plan = await _planner.CreatePlanAsync(Scan(first, second), [first, second], _destination.Root, new BackupOptions());

        var paths = plan.Items.Select(i => i.DestinationRelativePath).ToList();
        Assert.Equal(2, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Unselected_tracks_are_not_planned()
    {
        var one = Track("AAAA.mp3", "1", new TrackMetadata { Title = "One" });
        var two = Track("BBBB.mp3", "2", new TrackMetadata { Title = "Two" });

        var plan = await _planner.CreatePlanAsync(Scan(one, two), [two], _destination.Root, new BackupOptions());

        var item = Assert.Single(plan.Items);
        Assert.Equal("Two", item.Track.Metadata.Title);
    }

    [Fact]
    public async Task Impossible_free_space_is_reported_as_a_problem()
    {
        // A track whose recorded size is absurd forces the margin check to fail.
        var real = Track("AAAA.mp3", "tiny", new TrackMetadata { Title = "Song" });
        var huge = real with { SizeBytes = long.MaxValue / 4 };

        var plan = await _planner.CreatePlanAsync(Scan(huge), [huge], _destination.Root, new BackupOptions());

        Assert.NotNull(plan.FreeSpaceProblem);
        Assert.Contains("free", plan.FreeSpaceProblem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reasonable_backup_passes_the_free_space_check()
    {
        var track = Track("AAAA.mp3", "tiny", new TrackMetadata { Title = "Song" });
        var plan = await _planner.CreatePlanAsync(Scan(track), [track], _destination.Root, new BackupOptions());
        Assert.Null(plan.FreeSpaceProblem);
    }
}
