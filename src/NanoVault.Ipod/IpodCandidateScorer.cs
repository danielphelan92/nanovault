namespace NanoVault.Ipod;

/// <summary>Observable facts about one volume, gathered read-only.</summary>
public sealed record IpodDetectionSignals
{
    public bool HasIpodControlFolder { get; init; }
    public bool HasMusicFolder { get; init; }
    public bool HasITunesDatabase { get; init; }
    public bool VolumeLabelContainsIpod { get; init; }
    public bool IsRemovable { get; init; }
}

/// <summary>
/// Scores how confident NanoVault is that a volume is an iPod. Multiple
/// signals are combined so a renamed volume label or unusual drive letter
/// never breaks detection.
/// </summary>
public static class IpodCandidateScorer
{
    public const int IpodControlWeight = 40;
    public const int MusicFolderWeight = 30;
    public const int DatabaseWeight = 20;
    public const int VolumeLabelWeight = 15;
    public const int RemovableWeight = 5;

    /// <summary>Minimum score to treat a volume as a usable iPod music source.</summary>
    public const int ConfidentThreshold = IpodControlWeight + MusicFolderWeight;

    /// <summary>
    /// Score at which the volume looks iPod-related but its music storage is
    /// not readable (for example disk use disabled).
    /// </summary>
    public const int PossibleThreshold = VolumeLabelWeight;

    public static (int Score, IReadOnlyList<string> Reasons) Score(IpodDetectionSignals signals)
    {
        var score = 0;
        var reasons = new List<string>();

        if (signals.HasIpodControlFolder)
        {
            score += IpodControlWeight;
            reasons.Add("iPod_Control folder present");
        }

        if (signals.HasMusicFolder)
        {
            score += MusicFolderWeight;
            reasons.Add("iPod_Control\\Music folder present");
        }

        if (signals.HasITunesDatabase)
        {
            score += DatabaseWeight;
            reasons.Add("iPod database present");
        }

        if (signals.VolumeLabelContainsIpod)
        {
            score += VolumeLabelWeight;
            reasons.Add("volume label mentions iPod");
        }

        if (signals.IsRemovable)
        {
            score += RemovableWeight;
            reasons.Add("removable volume");
        }

        return (score, reasons);
    }

    public static bool IsConfident(int score) => score >= ConfidentThreshold;

    public static bool IsPossible(int score) => score >= PossibleThreshold;
}
