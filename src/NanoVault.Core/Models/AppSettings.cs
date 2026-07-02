namespace NanoVault.Core.Models;

/// <summary>
/// User preferences persisted under the local application data folder.
/// </summary>
public sealed record AppSettings
{
    public OrganizationTemplate Template { get; init; } = OrganizationTemplate.ArtistAlbum;
    public DuplicateBehavior DuplicateBehavior { get; init; } = DuplicateBehavior.SkipExactDuplicates;
    public bool VerifyCopies { get; init; } = true;
    public bool PreserveTimestamps { get; init; } = true;
    public bool IncludeProtectedFiles { get; init; } = true;
    public bool CreateMasterPlaylist { get; init; } = true;
    public bool CreateIpodPlaylists { get; init; } = true;
    public bool RememberLastDestination { get; init; } = true;
    public string? LastDestination { get; init; }
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Off by default. No network telemetry exists in this version.</summary>
    public bool AnonymousDiagnostics { get; init; }

    public BackupOptions ToBackupOptions() => new()
    {
        Template = Template,
        DuplicateBehavior = DuplicateBehavior,
        VerifyCopies = VerifyCopies,
        PreserveTimestamps = PreserveTimestamps,
        IncludeProtectedFiles = IncludeProtectedFiles,
        CreateMasterPlaylist = CreateMasterPlaylist,
        CreateIpodPlaylists = CreateIpodPlaylists,
    };
}
