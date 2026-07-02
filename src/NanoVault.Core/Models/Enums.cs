namespace NanoVault.Core.Models;

/// <summary>How the destination folder tree is organised.</summary>
public enum OrganizationTemplate
{
    /// <summary>Artist\Album\01 - Track Title.ext (default).</summary>
    ArtistAlbum = 0,

    /// <summary>Album Artist\Year - Album\01 - Track Title.ext.</summary>
    AlbumArtistYearAlbum = 1,

    /// <summary>All Music\Artist - Track Title.ext.</summary>
    FlatAllMusic = 2,
}

/// <summary>What to do when a destination file already exists.</summary>
public enum DuplicateBehavior
{
    /// <summary>Skip files whose destination already holds identical content (default).</summary>
    SkipExactDuplicates = 0,

    /// <summary>Keep both files by appending " (2)", " (3)", … to the new copy.</summary>
    KeepBoth = 1,

    /// <summary>Replace the destination file. Only ever chosen explicitly by the user.</summary>
    ReplaceDestination = 2,
}

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Readability classification of a discovered source file.</summary>
public enum TrackReadStatus
{
    /// <summary>File opened and is a supported, unprotected audio format.</summary>
    Readable = 0,

    /// <summary>Likely DRM-protected (for example .m4p). Copied as-is, never decrypted.</summary>
    Protected = 1,

    /// <summary>The file could not be opened or read.</summary>
    Unreadable = 2,
}

/// <summary>Where a merged metadata field ultimately came from.</summary>
[Flags]
public enum MetadataSources
{
    None = 0,
    EmbeddedTags = 1,
    IpodDatabase = 2,
    FallbackNaming = 4,
}

/// <summary>Planned handling for a single track in a backup plan.</summary>
public enum PlanItemAction
{
    Copy = 0,
    SkipExactDuplicate = 1,
    SkipUnreadable = 2,
    SkipProtectedExcluded = 3,
    ReplaceExisting = 4,
}

/// <summary>Final outcome for one track after the backup ran.</summary>
public enum TrackOutcome
{
    NotAttempted = 0,
    Copied = 1,
    SkippedDuplicate = 2,
    SkippedProtected = 3,
    SkippedUnreadable = 4,
    Failed = 5,
}

/// <summary>Overall state reported while a backup runs.</summary>
public enum BackupState
{
    NotStarted = 0,
    Copying = 1,
    Paused = 2,
    Verifying = 3,
    WritingReports = 4,
    Completed = 5,
    Cancelled = 6,
    Interrupted = 7,
    Faulted = 8,
}

public enum WarningSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}
