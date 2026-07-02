using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;

namespace NanoVault.Infrastructure.Devices;

/// <summary>
/// Fallback monitor that compares the set of mounted volume roots on a timer.
/// Works everywhere and never touches volume contents.
/// </summary>
public sealed class PollingVolumeMonitor : IVolumeMonitor
{
    private readonly IVolumeInfoProvider _volumes;
    private readonly ILogger _logger;
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private HashSet<string> _knownRoots = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public PollingVolumeMonitor(
        IVolumeInfoProvider volumes,
        ILogger<PollingVolumeMonitor> logger,
        TimeSpan? interval = null)
    {
        _volumes = volumes;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(2);
    }

    public event EventHandler? VolumesChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _knownRoots = SnapshotRoots();
        _timer ??= new Timer(_ => Poll(), null, _interval, _interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Poll()
    {
        try
        {
            var current = SnapshotRoots();
            if (!current.SetEquals(_knownRoots))
            {
                _knownRoots = current;
                VolumesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Volume poll failed");
        }
    }

    private HashSet<string> SnapshotRoots() =>
        _volumes.GetMountedVolumes()
            .Select(v => v.RootPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}

/// <summary>
/// Windows volume-arrival monitor using WMI (Win32_VolumeChangeEvent), with a
/// transparent polling fallback when WMI is unavailable. Watchers are always
/// disposed; nothing keeps accessing devices after the app closes.
/// </summary>
public sealed class WindowsVolumeMonitor : IVolumeMonitor
{
    private readonly ILogger<WindowsVolumeMonitor> _logger;
    private readonly PollingVolumeMonitor _fallback;
    private System.Management.ManagementEventWatcher? _watcher;
    private bool _usingFallback;
    private bool _disposed;

    public WindowsVolumeMonitor(
        IVolumeInfoProvider volumes,
        ILogger<WindowsVolumeMonitor> logger,
        ILogger<PollingVolumeMonitor> fallbackLogger)
    {
        _logger = logger;
        _fallback = new PollingVolumeMonitor(volumes, fallbackLogger, TimeSpan.FromSeconds(3));
        _fallback.VolumesChanged += (_, _) => VolumesChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? VolumesChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                StartWmi();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WMI volume notifications unavailable; using polling instead");
            }
        }

        _usingFallback = true;
        _fallback.Start();
    }

    [SupportedOSPlatform("windows")]
    private void StartWmi()
    {
        if (_watcher is not null)
        {
            return;
        }

        var query = new System.Management.WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent");
        _watcher = new System.Management.ManagementEventWatcher(query);
        _watcher.EventArrived += (_, _) => VolumesChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Start();
        _logger.LogInformation("Watching for volume arrival/removal via WMI");
    }

    public void Stop()
    {
        if (_usingFallback)
        {
            _fallback.Stop();
            return;
        }

        StopWmi();
    }

    private void StopWmi()
    {
        if (_watcher is null)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _watcher.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WMI watcher stop failed");
            }
        }

        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        _disposed = true;
        StopWmi();
        _fallback.Dispose();
    }
}
