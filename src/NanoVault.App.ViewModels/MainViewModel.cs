using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;
using NanoVault.Core.Utilities;

namespace NanoVault.App.ViewModels;

/// <summary>
/// Coordinates the whole workflow: watch for iPods, scan in the background,
/// plan, run the backup with pause/cancel, and land on the completion screen.
/// Holds no filesystem or Windows API code — only services.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IIpodDiscoveryService _discovery;
    private readonly IIpodScanner _scanner;
    private readonly IBackupPlanner _planner;
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;
    private readonly IVolumeMonitor _volumeMonitor;
    private readonly IFolderPicker _folderPicker;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;

    private CancellationTokenSource? _backupCts;
    private CancellationTokenSource? _scanCts;
    private PauseTokenSource? _pauseSource;
    private bool _disposed;

    public MainViewModel(
        IIpodDiscoveryService discovery,
        IIpodScanner scanner,
        IBackupPlanner planner,
        IBackupService backupService,
        ISettingsService settingsService,
        IVolumeMonitor volumeMonitor,
        IFolderPicker folderPicker,
        IShellService shell,
        IUiDispatcher dispatcher,
        ILogger<MainViewModel> logger)
    {
        _discovery = discovery;
        _scanner = scanner;
        _planner = planner;
        _backupService = backupService;
        _settingsService = settingsService;
        _volumeMonitor = volumeMonitor;
        _folderPicker = folderPicker;
        _shell = shell;
        _dispatcher = dispatcher;
        _logger = logger;

        Tracks = new TrackListViewModel();
        Settings = new SettingsViewModel(settingsService);
        Settings.Saved += OnSettingsSaved;

        _volumeMonitor.VolumesChanged += OnVolumesChanged;
    }

    public TrackListViewModel Tracks { get; }
    public SettingsViewModel Settings { get; }

    // ------------------------------------------------------------- state

    [ObservableProperty]
    private AppScreen _currentScreen = AppScreen.Home;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReviewTracksCommand))]
    private DeviceCardState _deviceState = DeviceCardState.Searching;

    public List<IpodDevice> Devices { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceSummary))]
    private IpodDevice? _selectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    private string? _destinationFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReviewTracksCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private string? _scanStatusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanSummary))]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReviewTracksCommand))]
    private ScanResult? _lastScan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanSummaryLines))]
    private BackupPlan? _currentPlan;

    [ObservableProperty]
    private string? _freeSpaceProblem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelBackupCommand))]
    private bool _isBackupRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    [NotifyPropertyChangedFor(nameof(CurrentTrackText))]
    [NotifyPropertyChangedFor(nameof(TracksProgressText))]
    [NotifyPropertyChangedFor(nameof(BytesProgressText))]
    [NotifyPropertyChangedFor(nameof(SpeedText))]
    [NotifyPropertyChangedFor(nameof(EtaText))]
    private BackupProgress _progress = new();

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private BackupResult? _lastResult;

    [ObservableProperty]
    private string? _fatalErrorMessage;

    [ObservableProperty]
    private string? _fatalErrorDetail;

    // -------------------------------------------------- display helpers

    public string DeviceSummary
    {
        get
        {
            if (SelectedDevice is not { } device)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (device.DriveLetter is not null)
            {
                parts.Add($"Drive {device.DriveLetter}:");
            }

            if (device.TotalCapacityBytes > 0)
            {
                parts.Add($"{ByteFormatter.FormatSize(device.TotalCapacityBytes)} capacity");
                parts.Add($"{ByteFormatter.FormatSize(device.FreeSpaceBytes)} free");
            }

            return string.Join("  ·  ", parts);
        }
    }

    public string ScanSummary
    {
        get
        {
            if (LastScan is not { } scan)
            {
                return string.Empty;
            }

            return $"{scan.Tracks.Count:N0} tracks  ·  {ByteFormatter.FormatSize(scan.TotalBytes)}";
        }
    }

    /// <summary>The pre-backup summary block, one line per fact.</summary>
    public IReadOnlyList<string> PlanSummaryLines
    {
        get
        {
            if (CurrentPlan is not { } plan)
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>
            {
                $"{plan.Items.Count:N0} tracks found",
                $"{ByteFormatter.FormatSize(plan.BytesToCopy)} to copy",
            };

            if (plan.SkippedDuplicateCount > 0)
            {
                lines.Add($"{plan.SkippedDuplicateCount:N0} exact duplicates will be skipped");
            }

            if (plan.ProtectedCopyCount > 0)
            {
                lines.Add($"{plan.ProtectedCopyCount:N0} protected files will be copied as-is");
            }

            if (plan.SkippedUnreadableCount > 0)
            {
                lines.Add($"{plan.SkippedUnreadableCount:N0} files could not be read");
            }

            return lines;
        }
    }

    public double ProgressPercent => Progress.Percent;
    public string ProgressPercentText => $"{Progress.Percent:0}%";
    public string CurrentTrackText => Progress.CurrentTrack ?? string.Empty;
    public string TracksProgressText => $"{Progress.CompletedTracks:N0} of {Progress.TotalTracks:N0} tracks";

    public string BytesProgressText =>
        $"{ByteFormatter.FormatSize(Progress.CopiedBytes)} of {ByteFormatter.FormatSize(Progress.TotalBytes)}";

    public string SpeedText => ByteFormatter.FormatSpeed(Progress.BytesPerSecond);

    public string EtaText => Progress.EstimatedRemaining is { } remaining
        ? $"About {ByteFormatter.FormatDuration(remaining)} left (estimate)"
        : string.Empty;

    // ------------------------------------------------------- lifecycle

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync().ConfigureAwait(true);
        Settings.LoadFrom(_settingsService.Current);

        if (_settingsService.Current is { RememberLastDestination: true, LastDestination: { Length: > 0 } last })
        {
            DestinationFolder = last;
        }

        _volumeMonitor.Start();
        await RefreshDevicesAsync().ConfigureAwait(true);
    }

    private void OnVolumesChanged(object? sender, EventArgs e) =>
        _dispatcher.Post(() =>
        {
            if (!IsBackupRunning)
            {
                _ = RefreshDevicesAsync();
            }
        });

    private void OnSettingsSaved(object? sender, AppSettings settings) =>
        _dispatcher.Post(() => _ = UpdatePlanAsync());

    // ------------------------------------------------------- discovery

    [RelayCommand]
    public async Task RefreshDevicesAsync()
    {
        if (IsBackupRunning)
        {
            return;
        }

        // A rescan supersedes any scan in flight.
        _scanCts?.Cancel();

        DeviceState = DeviceCardState.Searching;
        FatalErrorMessage = null;

        IReadOnlyList<IpodDevice> found;
        try
        {
            found = await _discovery.FindIpodsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device discovery failed");
            found = Array.Empty<IpodDevice>();
        }

        Devices = found.ToList();
        OnPropertyChanged(nameof(Devices));

        var usable = found.Where(d => d.HasMusicFolder).ToList();

        if (usable.Count == 1)
        {
            SelectedDevice = usable[0];
            DeviceState = DeviceCardState.Found;
            await ScanAsync(usable[0]).ConfigureAwait(true);
        }
        else if (usable.Count > 1)
        {
            SelectedDevice = null;
            LastScan = null;
            CurrentPlan = null;
            DeviceState = DeviceCardState.MultipleFound;
        }
        else if (found.Count > 0)
        {
            SelectedDevice = found[0];
            LastScan = null;
            CurrentPlan = null;
            DeviceState = DeviceCardState.StorageUnavailable;
        }
        else
        {
            SelectedDevice = null;
            LastScan = null;
            CurrentPlan = null;
            DeviceState = DeviceCardState.NotFound;
        }
    }

    [RelayCommand]
    public async Task SelectDeviceAsync(IpodDevice device)
    {
        SelectedDevice = device;
        DeviceState = DeviceCardState.Found;
        await ScanAsync(device).ConfigureAwait(true);
    }

    private async Task ScanAsync(IpodDevice device)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        ScanStatusText = "Looking through your music…";

        var progress = new UiProgress<ScanProgress>(_dispatcher, p =>
            ScanStatusText = p.FilesTotalEstimate > 0
                ? $"Reading track {p.FilesSeen:N0} of {p.FilesTotalEstimate:N0}…"
                : "Looking through your music…");

        try
        {
            var scan = await _scanner.ScanAsync(device, progress, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            LastScan = scan;
            Tracks.Load(scan);
            await UpdatePlanAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // A newer scan or rescan took over.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            LastScan = null;
            DeviceState = _selectedDeviceStillPresent() ? DeviceCardState.StorageUnavailable : DeviceCardState.NotFound;
        }
        finally
        {
            IsScanning = false;
            ScanStatusText = null;
        }

        bool _selectedDeviceStillPresent() => SelectedDevice is not null;
    }

    // -------------------------------------------------------- planning

    [RelayCommand]
    public void ChooseDestination()
    {
        var picked = _folderPicker.PickFolder(DestinationFolder);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            DestinationFolder = picked;
        }
    }

    partial void OnDestinationFolderChanged(string? value) => _ = UpdatePlanAsync();

    public async Task UpdatePlanAsync()
    {
        if (LastScan is not { } scan || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            CurrentPlan = null;
            FreeSpaceProblem = null;
            return;
        }

        try
        {
            var plan = await _planner.CreatePlanAsync(
                scan,
                Tracks.GetSelectedTracks(),
                DestinationFolder,
                _settingsService.Current.ToBackupOptions()).ConfigureAwait(true);

            CurrentPlan = plan;
            FreeSpaceProblem = plan.FreeSpaceProblem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Planning failed");
            CurrentPlan = null;
            FreeSpaceProblem = "The backup folder could not be checked. Choose a different folder.";
        }

        StartBackupCommand.NotifyCanExecuteChanged();
    }

    private bool CanReviewTracks() =>
        LastScan is { Tracks.Count: > 0 } && !IsScanning && DeviceState == DeviceCardState.Found;

    [RelayCommand(CanExecute = nameof(CanReviewTracks))]
    private void ReviewTracks() => CurrentScreen = AppScreen.Tracks;

    [RelayCommand]
    private async Task FinishReviewAsync()
    {
        CurrentScreen = AppScreen.Home;
        await UpdatePlanAsync().ConfigureAwait(true);
    }

    // ---------------------------------------------------------- backup

    private bool CanStartBackup() =>
        !IsBackupRunning
        && !IsScanning
        && DeviceState == DeviceCardState.Found
        && LastScan is { Tracks.Count: > 0 }
        && !string.IsNullOrWhiteSpace(DestinationFolder);

    [RelayCommand(CanExecute = nameof(CanStartBackup))]
    public async Task StartBackupAsync()
    {
        if (LastScan is not { } scan || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return;
        }

        FatalErrorMessage = null;
        FatalErrorDetail = null;

        // Plan freshly so settings, selection, and destination are current.
        await UpdatePlanAsync().ConfigureAwait(true);

        if (CurrentPlan is not { } plan)
        {
            return;
        }

        if (plan.FreeSpaceProblem is not null)
        {
            FreeSpaceProblem = plan.FreeSpaceProblem;
            return;
        }

        if (_settingsService.Current.RememberLastDestination)
        {
            await _settingsService.SaveAsync(_settingsService.Current with { LastDestination = DestinationFolder })
                .ConfigureAwait(true);
        }

        _backupCts = new CancellationTokenSource();
        _pauseSource = new PauseTokenSource();
        IsPaused = false;
        IsBackupRunning = true;
        Progress = new BackupProgress
        {
            State = BackupState.Copying,
            TotalTracks = plan.CopyCount,
            TotalBytes = plan.BytesToCopy,
        };
        CurrentScreen = AppScreen.Progress;

        var progress = new UiProgress<BackupProgress>(_dispatcher, p => Progress = p);

        try
        {
            var result = await _backupService
                .RunAsync(plan, progress, _pauseSource.Token, _backupCts.Token)
                .ConfigureAwait(true);

            LastResult = result;

            if (result.WasInterrupted)
            {
                DeviceState = DeviceCardState.DisconnectedDuringBackup;
            }

            CurrentScreen = AppScreen.Completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed with a fatal error");
            FatalErrorMessage = "The backup could not be completed. Your iPod has not been changed, " +
                                "and any tracks that finished copying are still in the backup folder.";
            FatalErrorDetail = ex.ToString();
            LastResult = null;
            CurrentScreen = AppScreen.Completion;
        }
        finally
        {
            IsBackupRunning = false;
            IsPaused = false;
            _backupCts?.Dispose();
            _backupCts = null;
            _pauseSource = null;
        }
    }

    private bool CanPauseResume() => IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private void PauseResume()
    {
        if (_pauseSource is not { } pause)
        {
            return;
        }

        if (pause.IsPaused)
        {
            pause.Resume();
            IsPaused = false;
        }
        else
        {
            pause.Pause();
            IsPaused = true;
        }
    }

    private bool CanCancelBackup() => IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanCancelBackup))]
    private void CancelBackup()
    {
        _pauseSource?.Resume();
        _backupCts?.Cancel();
    }

    // ------------------------------------------------------ completion

    [RelayCommand]
    private void OpenBackupFolder()
    {
        if (LastResult?.DestinationRoot is { Length: > 0 } root)
        {
            _shell.OpenFolder(root);
        }
        else if (DestinationFolder is { Length: > 0 } destination)
        {
            _shell.OpenFolder(destination);
        }
    }

    [RelayCommand]
    private void ViewReport()
    {
        if (LastResult?.HtmlReportPath is { Length: > 0 } report)
        {
            _shell.OpenFile(report);
        }
    }

    [RelayCommand]
    public async Task BackUpAnotherAsync()
    {
        LastResult = null;
        FatalErrorMessage = null;
        FatalErrorDetail = null;
        CurrentScreen = AppScreen.Home;
        await RefreshDevicesAsync().ConfigureAwait(true);
    }

    // ------------------------------------------------------ navigation

    [RelayCommand]
    private void OpenSettings() => CurrentScreen = AppScreen.Settings;

    [RelayCommand]
    private void OpenTroubleshooting() => CurrentScreen = AppScreen.Troubleshooting;

    [RelayCommand]
    private void GoHome() => CurrentScreen = AppScreen.Home;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _volumeMonitor.VolumesChanged -= OnVolumesChanged;
        _volumeMonitor.Dispose();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _backupCts?.Cancel();
        _backupCts?.Dispose();
    }

    /// <summary>Progress that marshals reports through the UI dispatcher.</summary>
    private sealed class UiProgress<T> : IProgress<T>
    {
        private readonly IUiDispatcher _dispatcher;
        private readonly Action<T> _apply;

        public UiProgress(IUiDispatcher dispatcher, Action<T> apply)
        {
            _dispatcher = dispatcher;
            _apply = apply;
        }

        public void Report(T value) => _dispatcher.Post(() => _apply(value));
    }
}
