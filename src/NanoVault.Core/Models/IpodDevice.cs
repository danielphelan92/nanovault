namespace NanoVault.Core.Models;

/// <summary>A mounted volume that NanoVault believes is an iPod.</summary>
public sealed record IpodDevice
{
    /// <summary>Volume root, for example "E:\".</summary>
    public required string RootPath { get; init; }

    public string? VolumeLabel { get; init; }

    /// <summary>Drive letter without the colon, when the volume has one.</summary>
    public string? DriveLetter { get; init; }

    public long TotalCapacityBytes { get; init; }
    public long FreeSpaceBytes { get; init; }

    /// <summary>Detection confidence from <c>IpodCandidateScorer</c>; higher is stronger.</summary>
    public int DetectionScore { get; init; }

    /// <summary>Human-readable reasons this volume was identified as an iPod.</summary>
    public IReadOnlyList<string> DetectionSignals { get; init; } = Array.Empty<string>();

    public bool HasMusicFolder { get; init; }
    public bool HasITunesDatabase { get; init; }

    /// <summary>Non-sensitive name shown in the UI. Never a serial number.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(VolumeLabel)
            ? (DriveLetter is null ? "iPod" : $"iPod ({DriveLetter}:)")
            : VolumeLabel!;
}
