using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.Ipod;

/// <summary>
/// Finds iPods among mounted volumes using several signals, never relying on
/// a fixed drive letter or volume label. All probing is read-only.
/// </summary>
public sealed class IpodDiscoveryService : IIpodDiscoveryService
{
    public const string IpodControlFolderName = "iPod_Control";
    public const string MusicFolderName = "Music";
    public const string ITunesFolderName = "iTunes";

    private static readonly string[] DatabaseFileNames = ["iTunesDB", "iTunesCDB"];

    private readonly IVolumeInfoProvider _volumes;
    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly ILogger<IpodDiscoveryService> _logger;

    public IpodDiscoveryService(
        IVolumeInfoProvider volumes,
        IReadOnlyFileSystem fileSystem,
        ILogger<IpodDiscoveryService> logger)
    {
        _volumes = volumes;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<IReadOnlyList<IpodDevice>> FindIpodsAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<IpodDevice>>(() =>
        {
            var found = new List<IpodDevice>();

            IReadOnlyList<VolumeInfo> volumes;
            try
            {
                volumes = _volumes.GetMountedVolumes();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Volume enumeration failed");
                return found;
            }

            foreach (var volume in volumes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Probe(volume) is { } device)
                    {
                        found.Add(device);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not inspect volume {Root}", volume.RootPath);
                }
            }

            return found
                .OrderByDescending(d => d.DetectionScore)
                .ThenBy(d => d.RootPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);

    private IpodDevice? Probe(VolumeInfo volume)
    {
        if (!volume.IsReady || string.IsNullOrEmpty(volume.RootPath))
        {
            return null;
        }

        var controlPath = CaseInsensitivePath.FindDirectory(_fileSystem, volume.RootPath, IpodControlFolderName);
        var musicPath = controlPath is null
            ? null
            : CaseInsensitivePath.FindDirectory(_fileSystem, controlPath, MusicFolderName);
        var itunesPath = controlPath is null
            ? null
            : CaseInsensitivePath.FindDirectory(_fileSystem, controlPath, ITunesFolderName);

        var hasDatabase = itunesPath is not null
            && DatabaseFileNames.Any(name => CaseInsensitivePath.FindFile(_fileSystem, itunesPath, name) is not null);

        var signals = new IpodDetectionSignals
        {
            HasIpodControlFolder = controlPath is not null,
            HasMusicFolder = musicPath is not null,
            HasITunesDatabase = hasDatabase,
            VolumeLabelContainsIpod = volume.VolumeLabel?.Contains("IPOD", StringComparison.OrdinalIgnoreCase) == true,
            IsRemovable = volume.IsRemovable,
        };

        var (score, reasons) = IpodCandidateScorer.Score(signals);
        if (!IpodCandidateScorer.IsPossible(score) || !signals.HasIpodControlFolder && !signals.VolumeLabelContainsIpod)
        {
            return null;
        }

        _logger.LogInformation(
            "Volume {Root} scored {Score} as iPod candidate ({Reasons})",
            volume.RootPath, score, string.Join(", ", reasons));

        return new IpodDevice
        {
            RootPath = volume.RootPath,
            VolumeLabel = volume.VolumeLabel,
            DriveLetter = ExtractDriveLetter(volume.RootPath),
            TotalCapacityBytes = volume.TotalSizeBytes,
            FreeSpaceBytes = volume.FreeSpaceBytes,
            DetectionScore = score,
            DetectionSignals = reasons,
            HasMusicFolder = signals.HasMusicFolder,
            HasITunesDatabase = signals.HasITunesDatabase,
        };
    }

    private static string? ExtractDriveLetter(string rootPath) =>
        rootPath.Length >= 2 && rootPath[1] == ':' && char.IsLetter(rootPath[0])
            ? char.ToUpperInvariant(rootPath[0]).ToString()
            : null;
}
