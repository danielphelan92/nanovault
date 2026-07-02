using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Policies;
using NanoVault.Core.Utilities;

namespace NanoVault.Infrastructure.Backup;

/// <summary>
/// Turns a scan into a reviewed plan: builds safe destination paths, applies
/// the duplicate policy, resolves collisions inside the plan itself, and
/// checks destination free space with a safety margin.
/// </summary>
public sealed class BackupPlanner : IBackupPlanner
{
    private const long MinimumSafetyMarginBytes = 256L * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly IDuplicateResolver _duplicateResolver;
    private readonly ILogger<BackupPlanner> _logger;

    public BackupPlanner(
        IFileSystem fileSystem,
        IDuplicateResolver duplicateResolver,
        ILogger<BackupPlanner> logger)
    {
        _fileSystem = fileSystem;
        _duplicateResolver = duplicateResolver;
        _logger = logger;
    }

    public async Task<BackupPlan> CreatePlanAsync(
        ScanResult scan,
        IReadOnlyCollection<DiscoveredTrack> selectedTracks,
        string destinationRoot,
        BackupOptions options,
        CancellationToken cancellationToken = default)
    {
        var selected = new HashSet<DiscoveredTrack>(selectedTracks);
        var items = new List<BackupPlanItem>(selectedTracks.Count);
        var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in scan.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!selected.Contains(track))
            {
                continue;
            }

            items.Add(await PlanTrackAsync(track, destinationRoot, options, claimedPaths, cancellationToken).ConfigureAwait(false));
        }

        var plan = new BackupPlan
        {
            Device = scan.Device,
            DestinationRoot = destinationRoot,
            Options = options,
            Items = items,
            Playlists = options.CreateIpodPlaylists ? scan.Playlists : Array.Empty<IpodPlaylist>(),
            Warnings = scan.Warnings,
        };

        var freeSpaceProblem = CheckFreeSpace(destinationRoot, plan.BytesToCopy);

        _logger.LogInformation(
            "Plan: {Copy} to copy ({Bytes}), {Duplicates} duplicates skipped, {Unreadable} unreadable",
            plan.CopyCount, ByteFormatter.FormatSize(plan.BytesToCopy),
            plan.SkippedDuplicateCount, plan.SkippedUnreadableCount);

        return plan with { FreeSpaceProblem = freeSpaceProblem };
    }

    private async Task<BackupPlanItem> PlanTrackAsync(
        DiscoveredTrack track,
        string destinationRoot,
        BackupOptions options,
        HashSet<string> claimedPaths,
        CancellationToken cancellationToken)
    {
        if (track.Status == TrackReadStatus.Unreadable)
        {
            return new BackupPlanItem
            {
                Track = track,
                Action = PlanItemAction.SkipUnreadable,
                Note = "This track could not be read from the iPod.",
            };
        }

        if (track.Status == TrackReadStatus.Protected && !options.IncludeProtectedFiles)
        {
            return new BackupPlanItem
            {
                Track = track,
                Action = PlanItemAction.SkipProtectedExcluded,
                Note = "Protected file excluded by settings.",
            };
        }

        var relativePath = OutputPathBuilder.BuildRelativePath(track, options);

        var decision = await _duplicateResolver
            .ResolveAsync(track, destinationRoot, relativePath, options.DuplicateBehavior, cancellationToken)
            .ConfigureAwait(false);

        var action = PlanItemAction.Copy;
        string? note = null;

        switch (decision.Outcome)
        {
            case DuplicateOutcome.ExactDuplicate:
                claimedPaths.Add(relativePath);
                return new BackupPlanItem
                {
                    Track = track,
                    DestinationRelativePath = relativePath,
                    Action = PlanItemAction.SkipExactDuplicate,
                    Note = "An identical copy is already in the backup folder.",
                };

            case DuplicateOutcome.UseAlternateName when decision.AlternateRelativePath is not null:
                relativePath = decision.AlternateRelativePath;
                note = "Renamed to avoid overwriting a different file with the same name.";
                break;

            case DuplicateOutcome.Replace:
                action = PlanItemAction.ReplaceExisting;
                note = "Will replace the existing destination file (chosen in settings).";
                break;
        }

        relativePath = ResolvePlanCollision(relativePath, claimedPaths);
        claimedPaths.Add(relativePath);

        if (track.Status == TrackReadStatus.Protected)
        {
            note = "Protected file: copied as-is, never decrypted. Playback may require the original authorised software.";
        }

        return new BackupPlanItem
        {
            Track = track,
            DestinationRelativePath = relativePath,
            Action = action,
            Note = note,
        };
    }

    /// <summary>
    /// Two different tracks can sanitise to the same destination (for example
    /// identical titles with missing track numbers). Suffix within the plan.
    /// </summary>
    private string ResolvePlanCollision(string relativePath, HashSet<string> claimedPaths)
    {
        if (!claimedPaths.Contains(relativePath))
        {
            return relativePath;
        }

        var directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var extension = Path.GetExtension(relativePath);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!claimedPaths.Contains(candidate) && !_fileSystem.FileExists(Path.Combine(candidate)))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }

    private string? CheckFreeSpace(string destinationRoot, long bytesToCopy)
    {
        try
        {
            var free = _fileSystem.GetAvailableFreeSpace(destinationRoot);
            var margin = Math.Max(MinimumSafetyMarginBytes, bytesToCopy / 100);
            var needed = bytesToCopy + margin;

            if (needed > free)
            {
                return $"The backup needs about {ByteFormatter.FormatSize(needed)} " +
                       $"(including a safety margin), but only {ByteFormatter.FormatSize(free)} is free " +
                       "at the destination. Free up space or choose another folder.";
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not determine free space for {Destination}", destinationRoot);
            return null;
        }
    }
}
