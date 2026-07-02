using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.App.ViewModels;
using NanoVault.Core.Models;
using Xunit;

namespace NanoVault.App.Tests;

public class MainViewModelTests
{
    private readonly FakeDiscovery _discovery = new();
    private readonly FakeScanner _scanner = new();
    private readonly FakePlanner _planner = new();
    private readonly FakeBackupService _backup = new();
    private readonly FakeSettingsService _settings = new();
    private readonly FakeVolumeMonitor _monitor = new();
    private readonly FakeFolderPicker _picker = new();
    private readonly FakeShell _shell = new();

    private MainViewModel Create() => new(
        _discovery, _scanner, _planner, _backup, _settings, _monitor,
        _picker, _shell, new ImmediateDispatcher(), NullLogger<MainViewModel>.Instance);

    // ------------------------------------------------------ device states

    [Fact]
    public async Task No_device_found_shows_not_found_state()
    {
        var vm = Create();
        await vm.InitializeAsync();

        Assert.Equal(DeviceCardState.NotFound, vm.DeviceState);
        Assert.Null(vm.SelectedDevice);
        Assert.True(_monitor.Started);
    }

    [Fact]
    public async Task Single_device_is_selected_and_scanned_automatically()
    {
        var device = TestData.Device();
        _discovery.Devices.Add(device);
        _scanner.Result = TestData.Scan(device, TestData.Track("A.mp3", "Song", "Artist"));

        var vm = Create();
        await vm.InitializeAsync();

        Assert.Equal(DeviceCardState.Found, vm.DeviceState);
        Assert.Equal(device, vm.SelectedDevice);
        Assert.NotNull(vm.LastScan);
        Assert.Equal(1, vm.Tracks.TotalCount);
        Assert.Equal(1, _scanner.ScanCount);
    }

    [Fact]
    public async Task Multiple_devices_wait_for_the_user_to_choose()
    {
        _discovery.Devices.Add(TestData.Device("/vol/one"));
        _discovery.Devices.Add(TestData.Device("/vol/two"));

        var vm = Create();
        await vm.InitializeAsync();

        Assert.Equal(DeviceCardState.MultipleFound, vm.DeviceState);
        Assert.Null(vm.SelectedDevice);
        Assert.Equal(0, _scanner.ScanCount);

        var chosen = _discovery.Devices[1];
        _scanner.Result = TestData.Scan(chosen, TestData.Track("B.mp3", "T"));
        await vm.SelectDeviceAsync(chosen);

        Assert.Equal(DeviceCardState.Found, vm.DeviceState);
        Assert.Equal(chosen, vm.SelectedDevice);
        Assert.Equal(1, _scanner.ScanCount);
    }

    [Fact]
    public async Task Unreadable_storage_shows_troubleshooting_state()
    {
        _discovery.Devices.Add(TestData.Device(hasMusic: false));

        var vm = Create();
        await vm.InitializeAsync();

        Assert.Equal(DeviceCardState.StorageUnavailable, vm.DeviceState);
    }

    [Fact]
    public async Task Volume_change_triggers_a_rescan()
    {
        var vm = Create();
        await vm.InitializeAsync();
        Assert.Equal(DeviceCardState.NotFound, vm.DeviceState);

        var device = TestData.Device();
        _discovery.Devices.Add(device);
        _scanner.Result = TestData.Scan(device, TestData.Track("A.mp3", "Song"));
        _monitor.RaiseChanged();
        await Task.Delay(50); // fire-and-forget refresh

        Assert.Equal(DeviceCardState.Found, vm.DeviceState);
    }

    // ------------------------------------------------------------- backup

    private async Task<MainViewModel> ReadyToBackupAsync()
    {
        var device = TestData.Device();
        _discovery.Devices.Add(device);
        _scanner.Result = TestData.Scan(device,
            TestData.Track("A.mp3", "One", "Artist"),
            TestData.Track("B.mp3", "Two", "Artist"));

        var vm = Create();
        await vm.InitializeAsync();

        _picker.NextPick = "/backup/folder";
        vm.ChooseDestination();
        await vm.UpdatePlanAsync();
        return vm;
    }

    [Fact]
    public async Task Backup_cannot_start_until_device_and_destination_exist()
    {
        var vm = Create();
        await vm.InitializeAsync();
        Assert.False(vm.StartBackupCommand.CanExecute(null));

        vm = await ReadyToBackupAsync();
        Assert.True(vm.StartBackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Successful_backup_walks_progress_then_completion()
    {
        var vm = await ReadyToBackupAsync();
        var screens = new List<AppScreen>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CurrentScreen))
            {
                screens.Add(vm.CurrentScreen);
            }
        };

        await vm.StartBackupAsync();

        Assert.Contains(AppScreen.Progress, screens);
        Assert.Equal(AppScreen.Completion, vm.CurrentScreen);
        Assert.NotNull(vm.LastResult);
        Assert.Equal(2, vm.LastResult!.CopiedCount);
        Assert.False(vm.IsBackupRunning);
        Assert.Null(vm.FatalErrorMessage);
    }

    [Fact]
    public async Task Backup_reports_progress_updates()
    {
        var vm = await ReadyToBackupAsync();
        _backup.Handler = (plan, progress, _, _) =>
        {
            progress?.Report(new BackupProgress
            {
                State = BackupState.Copying,
                TotalTracks = 2,
                CompletedTracks = 1,
                TotalBytes = 2000,
                CopiedBytes = 1000,
                CurrentTrack = "Artist – One",
            });

            return Task.FromResult(new BackupResult
            {
                DestinationRoot = plan.DestinationRoot,
                DeviceName = "iPod",
                FinalState = BackupState.Completed,
            });
        };

        await vm.StartBackupAsync();

        Assert.Equal(50, vm.Progress.Percent);
        Assert.Equal("Artist – One", vm.Progress.CurrentTrack);
    }

    [Fact]
    public async Task Cancelled_backup_lands_on_completion_with_cancelled_result()
    {
        var vm = await ReadyToBackupAsync();
        _backup.Handler = async (plan, _, _, ct) =>
        {
            vm.CancelBackupCommand.Execute(null);
            await Task.Delay(10, CancellationToken.None);
            Assert.True(ct.IsCancellationRequested);
            return new BackupResult
            {
                DestinationRoot = plan.DestinationRoot,
                DeviceName = "iPod",
                FinalState = BackupState.Cancelled,
            };
        };

        await vm.StartBackupAsync();

        Assert.Equal(AppScreen.Completion, vm.CurrentScreen);
        Assert.True(vm.LastResult!.WasCancelled);
    }

    [Fact]
    public async Task Pause_and_resume_toggle_the_pause_token()
    {
        var vm = await ReadyToBackupAsync();
        var pauseObserved = false;
        var resumed = new TaskCompletionSource();

        _backup.Handler = async (plan, _, pause, _) =>
        {
            while (!pause.IsPaused)
            {
                await Task.Delay(5);
            }

            pauseObserved = true;
            await pause.WaitWhilePausedAsync();
            resumed.TrySetResult();
            return new BackupResult
            {
                DestinationRoot = plan.DestinationRoot,
                DeviceName = "iPod",
                FinalState = BackupState.Completed,
            };
        };

        var run = vm.StartBackupAsync();
        while (!vm.IsBackupRunning)
        {
            await Task.Delay(5);
        }

        vm.PauseResumeCommand.Execute(null);
        Assert.True(vm.IsPaused);

        while (!pauseObserved)
        {
            await Task.Delay(5);
        }

        vm.PauseResumeCommand.Execute(null);
        Assert.False(vm.IsPaused);

        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await run;
    }

    [Fact]
    public async Task Completed_with_warnings_keeps_the_reassuring_completion_state()
    {
        var vm = await ReadyToBackupAsync();
        _backup.Handler = (plan, _, _, _) => Task.FromResult(new BackupResult
        {
            DestinationRoot = plan.DestinationRoot,
            DeviceName = "iPod",
            FinalState = BackupState.Completed,
            Warnings = [new BackupWarning("One track could not be read.", Severity: WarningSeverity.Error)],
        });

        await vm.StartBackupAsync();

        Assert.Equal(AppScreen.Completion, vm.CurrentScreen);
        Assert.False(vm.LastResult!.WasCancelled);
        Assert.Equal(1, vm.LastResult.WarningCount);
        Assert.Null(vm.FatalErrorMessage);
    }

    [Fact]
    public async Task Fatal_destination_error_shows_friendly_message_with_details()
    {
        var vm = await ReadyToBackupAsync();
        _backup.Handler = (_, _, _, _) =>
            Task.FromException<BackupResult>(new IOException("There is not enough space on the disk."));

        await vm.StartBackupAsync();

        Assert.Equal(AppScreen.Completion, vm.CurrentScreen);
        Assert.NotNull(vm.FatalErrorMessage);
        Assert.Contains("iPod has not been changed", vm.FatalErrorMessage);
        Assert.Contains("not enough space", vm.FatalErrorDetail);
        Assert.False(vm.IsBackupRunning);
    }

    [Fact]
    public async Task Interrupted_backup_flags_the_disconnected_device_state()
    {
        var vm = await ReadyToBackupAsync();
        _backup.Handler = (plan, _, _, _) => Task.FromResult(new BackupResult
        {
            DestinationRoot = plan.DestinationRoot,
            DeviceName = "iPod",
            FinalState = BackupState.Interrupted,
        });

        await vm.StartBackupAsync();

        Assert.Equal(DeviceCardState.DisconnectedDuringBackup, vm.DeviceState);
        Assert.True(vm.LastResult!.WasInterrupted);
    }

    [Fact]
    public async Task Free_space_problem_blocks_the_backup()
    {
        var vm = await ReadyToBackupAsync();
        _planner.FreeSpaceProblem = "Not enough space.";

        await vm.StartBackupAsync();

        Assert.Equal(AppScreen.Home, vm.CurrentScreen);
        Assert.Equal("Not enough space.", vm.FreeSpaceProblem);
        Assert.Null(vm.LastResult);
    }

    // -------------------------------------------------------- completion

    [Fact]
    public async Task Completion_buttons_open_folder_and_report()
    {
        var vm = await ReadyToBackupAsync();
        await vm.StartBackupAsync();

        vm.OpenBackupFolderCommand.Execute(null);
        Assert.Contains("/backup/folder", _shell.OpenedFolders);
    }

    [Fact]
    public async Task Back_up_another_returns_home_and_rescans()
    {
        var vm = await ReadyToBackupAsync();
        await vm.StartBackupAsync();
        var scansBefore = _scanner.ScanCount;

        await vm.BackUpAnotherAsync();

        Assert.Equal(AppScreen.Home, vm.CurrentScreen);
        Assert.Null(vm.LastResult);
        Assert.True(_scanner.ScanCount > scansBefore);
    }

    // ---------------------------------------------------------- settings

    [Fact]
    public async Task Destination_is_remembered_when_enabled()
    {
        var vm = await ReadyToBackupAsync();
        await vm.StartBackupAsync();

        Assert.Equal("/backup/folder", _settings.Current.LastDestination);
    }

    [Fact]
    public async Task Navigation_commands_move_between_screens()
    {
        var vm = Create();
        await vm.InitializeAsync();

        vm.OpenSettingsCommand.Execute(null);
        Assert.Equal(AppScreen.Settings, vm.CurrentScreen);

        vm.GoHomeCommand.Execute(null);
        Assert.Equal(AppScreen.Home, vm.CurrentScreen);

        vm.OpenTroubleshootingCommand.Execute(null);
        Assert.Equal(AppScreen.Troubleshooting, vm.CurrentScreen);
    }

    [Fact]
    public async Task Dispose_stops_and_disposes_the_volume_monitor()
    {
        var vm = Create();
        await vm.InitializeAsync();

        vm.Dispose();

        Assert.True(_monitor.Disposed);
    }
}
