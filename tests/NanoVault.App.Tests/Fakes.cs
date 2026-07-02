using NanoVault.App.ViewModels;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;

namespace NanoVault.App.Tests;

public sealed class FakeDiscovery : IIpodDiscoveryService
{
    public List<IpodDevice> Devices { get; } = new();
    public Exception? Throws { get; set; }

    public Task<IReadOnlyList<IpodDevice>> FindIpodsAsync(CancellationToken cancellationToken = default) =>
        Throws is { } ex
            ? Task.FromException<IReadOnlyList<IpodDevice>>(ex)
            : Task.FromResult<IReadOnlyList<IpodDevice>>(Devices.ToList());
}

public sealed class FakeScanner : IIpodScanner
{
    public ScanResult? Result { get; set; }
    public Exception? Throws { get; set; }
    public int ScanCount { get; private set; }

    public Task<ScanResult> ScanAsync(
        IpodDevice device, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ScanCount++;
        if (Throws is { } ex)
        {
            return Task.FromException<ScanResult>(ex);
        }

        return Task.FromResult(Result ?? new ScanResult { Device = device });
    }
}

public sealed class FakePlanner : IBackupPlanner
{
    public string? FreeSpaceProblem { get; set; }
    public Exception? Throws { get; set; }

    public Task<BackupPlan> CreatePlanAsync(
        ScanResult scan,
        IReadOnlyCollection<DiscoveredTrack> selectedTracks,
        string destinationRoot,
        BackupOptions options,
        CancellationToken cancellationToken = default)
    {
        if (Throws is { } ex)
        {
            return Task.FromException<BackupPlan>(ex);
        }

        var items = scan.Tracks
            .Where(selectedTracks.Contains)
            .Select(t => new BackupPlanItem
            {
                Track = t,
                DestinationRelativePath = Path.GetFileName(t.SourcePath),
            })
            .ToList();

        return Task.FromResult(new BackupPlan
        {
            Device = scan.Device,
            DestinationRoot = destinationRoot,
            Options = options,
            Items = items,
            FreeSpaceProblem = FreeSpaceProblem,
        });
    }
}

public sealed class FakeBackupService : IBackupService
{
    public Func<BackupPlan, IProgress<BackupProgress>?, PauseToken, CancellationToken, Task<BackupResult>>? Handler { get; set; }

    public Task<BackupResult> RunAsync(
        BackupPlan plan,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default)
    {
        if (Handler is { } handler)
        {
            return handler(plan, progress, pause, cancellationToken);
        }

        return Task.FromResult(new BackupResult
        {
            DestinationRoot = plan.DestinationRoot,
            DeviceName = plan.Device.DisplayName,
            FinalState = BackupState.Completed,
            TrackResults = plan.Items.Select(i => new TrackBackupResult
            {
                Track = i.Track,
                Outcome = TrackOutcome.Copied,
                FinalRelativePath = i.DestinationRelativePath,
                BytesCopied = i.Track.SizeBytes,
                Verified = true,
            }).ToList(),
        });
    }
}

public sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Current = settings;
        SettingsChanged?.Invoke(this, settings);
        return Task.CompletedTask;
    }
}

public sealed class FakeVolumeMonitor : IVolumeMonitor
{
    public bool Started { get; private set; }
    public bool Disposed { get; private set; }

    public event EventHandler? VolumesChanged;

    public void Start() => Started = true;

    public void Stop() => Started = false;

    public void RaiseChanged() => VolumesChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Disposed = true;
}

public sealed class FakeFolderPicker : IFolderPicker
{
    public string? NextPick { get; set; }

    public string? PickFolder(string? initialFolder) => NextPick;
}

public sealed class FakeShell : IShellService
{
    public List<string> OpenedFolders { get; } = new();
    public List<string> OpenedFiles { get; } = new();

    public void OpenFolder(string path) => OpenedFolders.Add(path);

    public void OpenFile(string path) => OpenedFiles.Add(path);
}

public static class TestData
{
    public static IpodDevice Device(string root = "/vol/ipod", bool hasMusic = true) => new()
    {
        RootPath = root,
        VolumeLabel = "IPOD",
        HasMusicFolder = hasMusic,
        DetectionScore = hasMusic ? 90 : 20,
        TotalCapacityBytes = 8L << 30,
        FreeSpaceBytes = 1L << 30,
    };

    public static DiscoveredTrack Track(string name, string? title = null, string? artist = null, string? genre = null) => new()
    {
        SourcePath = $"/vol/ipod/iPod_Control/Music/F00/{name}",
        RelativeSourcePath = $"iPod_Control/Music/F00/{name}",
        SizeBytes = 1000,
        Extension = Path.GetExtension(name),
        Metadata = new TrackMetadata { Title = title, Artist = artist, Genre = genre },
    };

    public static ScanResult Scan(IpodDevice device, params DiscoveredTrack[] tracks) => new()
    {
        Device = device,
        Tracks = tracks,
    };
}
