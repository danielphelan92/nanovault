using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;

namespace NanoVault.Infrastructure.Backup;

/// <summary>
/// The complete backup workflow: run the plan, then write playlists and the
/// backup report. Reports are written even after cancellation or interruption
/// so the user always knows exactly what was backed up.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IBackupExecutor _executor;
    private readonly IPlaylistWriter _playlistWriter;
    private readonly IBackupReportWriter _reportWriter;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IBackupExecutor executor,
        IPlaylistWriter playlistWriter,
        IBackupReportWriter reportWriter,
        ILogger<BackupService> logger)
    {
        _executor = executor;
        _playlistWriter = playlistWriter;
        _reportWriter = reportWriter;
        _logger = logger;
    }

    public async Task<BackupResult> RunAsync(
        BackupPlan plan,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync(plan, progress, pause, cancellationToken).ConfigureAwait(false);
        var warnings = new List<BackupWarning>(result.Warnings);

        progress?.Report(new BackupProgress
        {
            State = BackupState.WritingReports,
            TotalTracks = plan.CopyCount,
            CompletedTracks = result.CopiedCount,
            TotalBytes = plan.BytesToCopy,
            CopiedBytes = result.TotalCopiedBytes,
        });

        // Post-run outputs never observe the user's cancel: after a cancelled
        // run the report is what tells them what did get copied.
        string? masterPlaylist = null;
        var playlistPaths = new List<string>();

        if (plan.Options.CreateMasterPlaylist)
        {
            try
            {
                masterPlaylist = await _playlistWriter
                    .WriteMasterPlaylistAsync(plan.DestinationRoot, result.TrackResults, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Master playlist could not be written");
                warnings.Add(new BackupWarning("The overall playlist could not be created.", Severity: WarningSeverity.Warning));
            }
        }

        if (plan.Options.CreateIpodPlaylists && plan.Playlists.Count > 0)
        {
            try
            {
                var written = await _playlistWriter
                    .WriteIpodPlaylistsAsync(plan.DestinationRoot, plan.Playlists, result.TrackResults, CancellationToken.None)
                    .ConfigureAwait(false);
                playlistPaths.AddRange(written);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "iPod playlists could not be written");
                warnings.Add(new BackupWarning("Playlists from the iPod could not be recreated.", Severity: WarningSeverity.Warning));
            }
        }

        result = result with
        {
            MasterPlaylistRelativePath = masterPlaylist,
            PlaylistRelativePaths = playlistPaths,
            Warnings = warnings,
        };

        try
        {
            var reportPaths = await _reportWriter.WriteAsync(result, CancellationToken.None).ConfigureAwait(false);
            result = result with
            {
                HtmlReportPath = reportPaths.HtmlPath,
                JsonReportPath = reportPaths.JsonPath,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Backup report could not be written");
            result = result with
            {
                Warnings = warnings.Append(
                    new BackupWarning("The backup report could not be written.", Severity: WarningSeverity.Warning)).ToList(),
            };
        }

        return result;
    }
}
