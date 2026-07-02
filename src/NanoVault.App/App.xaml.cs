using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NanoVault.App.Services;
using NanoVault.App.ViewModels;
using NanoVault.Core.Abstractions;
using NanoVault.Infrastructure.Backup;
using NanoVault.Infrastructure.Devices;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Metadata;
using NanoVault.Infrastructure.Playlists;
using NanoVault.Infrastructure.Reports;
using NanoVault.Infrastructure.Settings;
using NanoVault.Infrastructure.Verification;
using NanoVault.Ipod;
using NanoVault.Ipod.Database;
using Serilog;

namespace NanoVault.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _services = ConfigureServices();

        var viewModel = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;

        ThemeManager.Apply(Core.Models.AppTheme.System);
        viewModel.Settings.Saved += (_, settings) => ThemeManager.Apply(settings.Theme);

        window.Show();

        try
        {
            await viewModel.InitializeAsync();
            ThemeManager.Apply(_services.GetRequiredService<ISettingsService>().Current.Theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup initialisation failed");
        }
    }

    private static void ConfigureLogging()
    {
        var logsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NanoVault", "Logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logsFolder, "nanovault-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        Log.Information("NanoVault starting");
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: false));

        // Filesystem: one physical implementation; iPod-facing services only
        // ever see the read-only interface.
        services.AddSingleton<PhysicalFileSystem>();
        services.AddSingleton<IFileSystem>(sp => sp.GetRequiredService<PhysicalFileSystem>());
        services.AddSingleton<IReadOnlyFileSystem>(sp => sp.GetRequiredService<PhysicalFileSystem>());

        // Devices and discovery.
        services.AddSingleton<IVolumeInfoProvider, DriveInfoVolumeProvider>();
        services.AddSingleton<IVolumeMonitor, WindowsVolumeMonitor>();
        services.AddSingleton<IIpodDiscoveryService, IpodDiscoveryService>();

        // Scanning and metadata.
        services.AddSingleton<IAudioMetadataReader, TagLibAudioMetadataReader>();
        services.AddSingleton<IIpodDatabaseReader, ITunesDbReader>();
        services.AddSingleton<IIpodScanner, IpodScanner>();

        // Planning, copying, outputs.
        services.AddSingleton<IFileVerificationService, Sha256FileVerificationService>();
        services.AddSingleton<IDuplicateResolver, DuplicateResolver>();
        services.AddSingleton<IBackupPlanner, BackupPlanner>();
        services.AddSingleton<ITrackCopyService, TrackCopyService>();
        services.AddSingleton<IBackupExecutor, BackupExecutor>();
        services.AddSingleton<IPlaylistWriter, M3u8PlaylistWriter>();
        services.AddSingleton<IBackupReportWriter, BackupReportWriter>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // UI plumbing.
        services.AddSingleton<IFolderPicker, WpfFolderPicker>();
        services.AddSingleton<IShellService, WindowsShellService>();
        services.AddSingleton<IUiDispatcher, WpfDispatcher>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
        MessageBox.Show(
            "Something went wrong, and the current operation was stopped.\n\n" +
            "Your iPod has not been changed, and any music already copied is still in your backup folder.\n\n" +
            $"Technical details: {e.Exception.Message}",
            "NanoVault",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        Log.Information("NanoVault closed");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
