namespace NanoVault.Core.Models;

/// <summary>Live progress for the backup screen.</summary>
public sealed record BackupProgress
{
    public BackupState State { get; init; } = BackupState.NotStarted;
    public int TotalTracks { get; init; }
    public int CompletedTracks { get; init; }
    public long TotalBytes { get; init; }
    public long CopiedBytes { get; init; }
    public string? CurrentTrack { get; init; }
    public double BytesPerSecond { get; init; }

    public double Percent => TotalBytes <= 0
        ? (TotalTracks <= 0 ? 0 : 100.0 * CompletedTracks / TotalTracks)
        : Math.Clamp(100.0 * CopiedBytes / TotalBytes, 0, 100);

    /// <summary>Rough remaining time; always presented to the user as an estimate.</summary>
    public TimeSpan? EstimatedRemaining =>
        BytesPerSecond > 1 && TotalBytes > CopiedBytes
            ? TimeSpan.FromSeconds((TotalBytes - CopiedBytes) / BytesPerSecond)
            : null;
}

public sealed record BackupWarning(string Message, string? TrackPath = null, WarningSeverity Severity = WarningSeverity.Warning);

/// <summary>Result for one track after the run.</summary>
public sealed record TrackBackupResult
{
    public required DiscoveredTrack Track { get; init; }
    public TrackOutcome Outcome { get; init; } = TrackOutcome.NotAttempted;
    public string? FinalRelativePath { get; init; }
    public long BytesCopied { get; init; }
    public string? Sha256 { get; init; }
    public bool Verified { get; init; }
    public string? Error { get; init; }

    /// <summary>Technical detail kept out of the main message but copyable.</summary>
    public string? TechnicalDetail { get; init; }
}

/// <summary>Everything that happened during one backup run.</summary>
public sealed record BackupResult
{
    public required string DestinationRoot { get; init; }
    public required string DeviceName { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public BackupState FinalState { get; init; } = BackupState.Completed;
    public IReadOnlyList<TrackBackupResult> TrackResults { get; init; } = Array.Empty<TrackBackupResult>();
    public IReadOnlyList<BackupWarning> Warnings { get; init; } = Array.Empty<BackupWarning>();
    public string? MasterPlaylistRelativePath { get; init; }
    public IReadOnlyList<string> PlaylistRelativePaths { get; init; } = Array.Empty<string>();
    public string? HtmlReportPath { get; init; }
    public string? JsonReportPath { get; init; }

    public TimeSpan Elapsed => FinishedAt - StartedAt;
    public int CopiedCount => TrackResults.Count(r => r.Outcome == TrackOutcome.Copied);
    public int SkippedDuplicateCount => TrackResults.Count(r => r.Outcome == TrackOutcome.SkippedDuplicate);
    public int FailedCount => TrackResults.Count(r => r.Outcome == TrackOutcome.Failed);
    public int NotAttemptedCount => TrackResults.Count(r => r.Outcome == TrackOutcome.NotAttempted);
    public int WarningCount => Warnings.Count(w => w.Severity != WarningSeverity.Info);
    public long TotalCopiedBytes => TrackResults.Sum(r => r.BytesCopied);

    public bool WasCancelled => FinalState == BackupState.Cancelled;
    public bool WasInterrupted => FinalState == BackupState.Interrupted;
}
