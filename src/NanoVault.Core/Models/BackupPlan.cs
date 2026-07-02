namespace NanoVault.Core.Models;

/// <summary>Options that shape one backup run. Snapshotted from settings.</summary>
public sealed record BackupOptions
{
    public OrganizationTemplate Template { get; init; } = OrganizationTemplate.ArtistAlbum;
    public DuplicateBehavior DuplicateBehavior { get; init; } = DuplicateBehavior.SkipExactDuplicates;
    public bool VerifyCopies { get; init; } = true;
    public bool PreserveTimestamps { get; init; } = true;
    public bool IncludeProtectedFiles { get; init; } = true;
    public bool CreateMasterPlaylist { get; init; } = true;
    public bool CreateIpodPlaylists { get; init; } = true;
    public bool UseDiscSubfolders { get; init; } = true;

    /// <summary>Concurrent track copies. Kept low to be gentle on old devices.</summary>
    public int MaxConcurrentCopies { get; init; } = 1;

    public int CopyBufferBytes { get; init; } = 1024 * 1024;
}

/// <summary>One track in the plan, with its resolved destination.</summary>
public sealed record BackupPlanItem
{
    public required DiscoveredTrack Track { get; init; }

    /// <summary>Destination path relative to the backup root; null when skipped.</summary>
    public string? DestinationRelativePath { get; init; }

    public PlanItemAction Action { get; init; } = PlanItemAction.Copy;

    /// <summary>Friendly explanation when the item is skipped.</summary>
    public string? Note { get; init; }

    public bool WillCopy => Action is PlanItemAction.Copy or PlanItemAction.ReplaceExisting;
}

/// <summary>The full reviewed plan for a backup run.</summary>
public sealed record BackupPlan
{
    public required IpodDevice Device { get; init; }
    public required string DestinationRoot { get; init; }
    public required BackupOptions Options { get; init; }
    public IReadOnlyList<BackupPlanItem> Items { get; init; } = Array.Empty<BackupPlanItem>();
    public IReadOnlyList<IpodPlaylist> Playlists { get; init; } = Array.Empty<IpodPlaylist>();
    public IReadOnlyList<ScanWarning> Warnings { get; init; } = Array.Empty<ScanWarning>();

    /// <summary>Free-space check outcome; null when the destination has room.</summary>
    public string? FreeSpaceProblem { get; init; }

    public int CopyCount => Items.Count(i => i.WillCopy);
    public int SkippedDuplicateCount => Items.Count(i => i.Action == PlanItemAction.SkipExactDuplicate);
    public int SkippedUnreadableCount => Items.Count(i => i.Action == PlanItemAction.SkipUnreadable);
    public int ProtectedCopyCount => Items.Count(i => i.WillCopy && i.Track.Status == TrackReadStatus.Protected);
    public long BytesToCopy => Items.Where(i => i.WillCopy).Sum(i => i.Track.SizeBytes);
}
