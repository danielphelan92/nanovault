using NanoVault.Core.Models;
using NanoVault.Core.Threading;

namespace NanoVault.Core.Abstractions;

/// <summary>Builds a reviewed plan: destinations, duplicates, free space.</summary>
public interface IBackupPlanner
{
    Task<BackupPlan> CreatePlanAsync(
        ScanResult scan,
        IReadOnlyCollection<DiscoveredTrack> selectedTracks,
        string destinationRoot,
        BackupOptions options,
        CancellationToken cancellationToken = default);
}

public enum DuplicateOutcome
{
    /// <summary>Nothing at the destination path.</summary>
    NoConflict = 0,

    /// <summary>Destination already holds identical content (size + SHA-256).</summary>
    ExactDuplicate = 1,

    /// <summary>Different content exists; copy under the supplied alternate name.</summary>
    UseAlternateName = 2,

    /// <summary>User explicitly chose to replace the destination file.</summary>
    Replace = 3,
}

public sealed record DuplicateDecision(DuplicateOutcome Outcome, string? AlternateRelativePath = null);

/// <summary>Decides what to do when a destination file already exists.</summary>
public interface IDuplicateResolver
{
    Task<DuplicateDecision> ResolveAsync(
        DiscoveredTrack track,
        string destinationRoot,
        string destinationRelativePath,
        DuplicateBehavior behavior,
        CancellationToken cancellationToken = default);
}

/// <summary>SHA-256 hashing and copy verification.</summary>
public interface IFileVerificationService
{
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
    Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>Context shared by all copies in one backup run.</summary>
public sealed record CopyContext
{
    public required BackupOptions Options { get; init; }
    public PauseToken Pause { get; init; } = PauseToken.None;

    /// <summary>Receives byte deltas as they are written, for live progress.</summary>
    public IProgress<long>? BytesProgress { get; init; }
}

/// <summary>Copies a single planned track safely (partial file, verify, atomic rename).</summary>
public interface ITrackCopyService
{
    Task<TrackBackupResult> CopyTrackAsync(
        BackupPlanItem item,
        string destinationRoot,
        CopyContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Runs a whole plan and aggregates the result.</summary>
public interface IBackupExecutor
{
    Task<BackupResult> ExecuteAsync(
        BackupPlan plan,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the complete backup workflow: executes the plan, then writes
/// playlists and reports, returning the final enriched result.
/// </summary>
public interface IBackupService
{
    Task<BackupResult> RunAsync(
        BackupPlan plan,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default);
}

public sealed record ReportPaths(string? HtmlPath, string? JsonPath);

/// <summary>Writes the human-readable HTML report and the JSON export.</summary>
public interface IBackupReportWriter
{
    Task<ReportPaths> WriteAsync(BackupResult result, CancellationToken cancellationToken = default);
}

/// <summary>Writes UTF-8 M3U8 playlists with relative paths.</summary>
public interface IPlaylistWriter
{
    /// <summary>Writes "All iPod Music.m3u8" in the backup root. Returns its relative path.</summary>
    Task<string?> WriteMasterPlaylistAsync(
        string destinationRoot,
        IReadOnlyList<TrackBackupResult> results,
        CancellationToken cancellationToken = default);

    /// <summary>Writes Playlists\&lt;name&gt;.m3u8 for each recovered iPod playlist.</summary>
    Task<IReadOnlyList<string>> WriteIpodPlaylistsAsync(
        string destinationRoot,
        IReadOnlyList<IpodPlaylist> playlists,
        IReadOnlyList<TrackBackupResult> results,
        CancellationToken cancellationToken = default);
}

/// <summary>Loads and saves user preferences.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    event EventHandler<AppSettings>? SettingsChanged;
}
