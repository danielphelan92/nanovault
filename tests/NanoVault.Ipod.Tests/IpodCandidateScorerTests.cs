using NanoVault.Ipod;
using Xunit;

namespace NanoVault.Ipod.Tests;

public class IpodCandidateScorerTests
{
    [Fact]
    public void Full_ipod_scores_all_signals()
    {
        var (score, reasons) = IpodCandidateScorer.Score(new IpodDetectionSignals
        {
            HasIpodControlFolder = true,
            HasMusicFolder = true,
            HasITunesDatabase = true,
            VolumeLabelContainsIpod = true,
            IsRemovable = true,
        });

        Assert.Equal(110, score);
        Assert.Equal(5, reasons.Count);
        Assert.True(IpodCandidateScorer.IsConfident(score));
    }

    [Fact]
    public void Folder_structure_alone_is_confident_without_label()
    {
        // A renamed volume label must never break detection.
        var (score, _) = IpodCandidateScorer.Score(new IpodDetectionSignals
        {
            HasIpodControlFolder = true,
            HasMusicFolder = true,
        });

        Assert.True(IpodCandidateScorer.IsConfident(score));
    }

    [Fact]
    public void Label_alone_is_possible_but_not_confident()
    {
        var (score, _) = IpodCandidateScorer.Score(new IpodDetectionSignals
        {
            VolumeLabelContainsIpod = true,
            IsRemovable = true,
        });

        Assert.False(IpodCandidateScorer.IsConfident(score));
        Assert.True(IpodCandidateScorer.IsPossible(score));
    }

    [Fact]
    public void Random_removable_drive_scores_nothing_meaningful()
    {
        var (score, _) = IpodCandidateScorer.Score(new IpodDetectionSignals { IsRemovable = true });
        Assert.False(IpodCandidateScorer.IsPossible(score));
    }

    [Fact]
    public void Ipod_control_without_music_is_not_confident()
    {
        var (score, _) = IpodCandidateScorer.Score(new IpodDetectionSignals
        {
            HasIpodControlFolder = true,
        });

        Assert.False(IpodCandidateScorer.IsConfident(score));
        Assert.True(IpodCandidateScorer.IsPossible(score));
    }
}
