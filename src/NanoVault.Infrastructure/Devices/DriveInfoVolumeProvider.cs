using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;

namespace NanoVault.Infrastructure.Devices;

/// <summary>
/// Enumerates mounted volumes with <see cref="DriveInfo"/>. Requires no
/// administrator rights and tolerates volumes that go away mid-enumeration.
/// </summary>
public sealed class DriveInfoVolumeProvider : IVolumeInfoProvider
{
    private readonly ILogger<DriveInfoVolumeProvider> _logger;

    public DriveInfoVolumeProvider(ILogger<DriveInfoVolumeProvider> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<VolumeInfo> GetMountedVolumes()
    {
        var volumes = new List<VolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                volumes.Add(new VolumeInfo
                {
                    RootPath = drive.RootDirectory.FullName,
                    VolumeLabel = SafeLabel(drive),
                    DriveFormat = drive.DriveFormat,
                    IsRemovable = drive.DriveType == DriveType.Removable,
                    IsReady = true,
                    TotalSizeBytes = drive.TotalSize,
                    FreeSpaceBytes = drive.AvailableFreeSpace,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Volume {Name} skipped during enumeration", drive.Name);
            }
        }

        return volumes;
    }

    private static string? SafeLabel(DriveInfo drive)
    {
        try
        {
            return string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
