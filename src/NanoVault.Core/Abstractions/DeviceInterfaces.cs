using NanoVault.Core.Models;

namespace NanoVault.Core.Abstractions;

/// <summary>A mounted volume as reported by the operating system.</summary>
public sealed record VolumeInfo
{
    /// <summary>Root path, for example "E:\".</summary>
    public required string RootPath { get; init; }

    public string? VolumeLabel { get; init; }
    public string? DriveFormat { get; init; }
    public bool IsRemovable { get; init; }
    public bool IsReady { get; init; } = true;
    public long TotalSizeBytes { get; init; }
    public long FreeSpaceBytes { get; init; }
}

/// <summary>Enumerates mounted volumes. Implemented per platform; faked in tests.</summary>
public interface IVolumeInfoProvider
{
    IReadOnlyList<VolumeInfo> GetMountedVolumes();
}

/// <summary>
/// Raises <see cref="VolumesChanged"/> when a volume arrives or is removed.
/// Implementations must be disposed and must stop all device access afterwards.
/// </summary>
public interface IVolumeMonitor : IDisposable
{
    event EventHandler? VolumesChanged;
    void Start();
    void Stop();
}

/// <summary>Finds connected iPods among the mounted volumes.</summary>
public interface IIpodDiscoveryService
{
    Task<IReadOnlyList<IpodDevice>> FindIpodsAsync(CancellationToken cancellationToken = default);
}
