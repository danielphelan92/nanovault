using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Utilities;

namespace NanoVault.Infrastructure.Reports;

/// <summary>
/// Writes a human-readable HTML report plus a JSON export into the backup
/// root. Only backup-relevant data is included — never serial numbers or
/// account details.
/// </summary>
public sealed class BackupReportWriter : IBackupReportWriter
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BackupReportWriter> _logger;

    public BackupReportWriter(IFileSystem fileSystem, ILogger<BackupReportWriter> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public async Task<ReportPaths> WriteAsync(BackupResult result, CancellationToken cancellationToken = default)
    {
        _fileSystem.CreateDirectory(result.DestinationRoot);

        var baseName = $"NanoVault Backup Report - {result.FinishedAt.ToLocalTime():yyyy-MM-dd}";
        var htmlPath = UniquePath(result.DestinationRoot, baseName, ".html");
        var jsonPath = UniquePath(result.DestinationRoot, baseName, ".json");

        await WriteTextAsync(htmlPath, BuildHtml(result), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(jsonPath, BuildJson(result), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Backup report written to {Html}", htmlPath);
        return new ReportPaths(htmlPath, jsonPath);
    }

    private string UniquePath(string root, string baseName, string extension)
    {
        var path = Path.Combine(root, baseName + extension);
        for (var i = 2; _fileSystem.FileExists(path) && i < 1000; i++)
        {
            path = Path.Combine(root, $"{baseName} ({i}){extension}");
        }

        return path;
    }

    private async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        var stream = _fileSystem.CreateWrite(path);
        await using (stream.ConfigureAwait(false))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- JSON

    private sealed record JsonReport(
        string Schema,
        string AppVersion,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        string Device,
        string Destination,
        string FinalState,
        JsonTotals Totals,
        IReadOnlyList<JsonTrack> Tracks,
        IReadOnlyList<JsonWarning> Warnings,
        string? MasterPlaylist,
        IReadOnlyList<string> Playlists);

    private sealed record JsonTotals(
        int TracksFound,
        int Copied,
        int SkippedDuplicates,
        int Failed,
        int NotAttempted,
        long CopiedBytes,
        double ElapsedSeconds);

    private sealed record JsonTrack(
        string Source,
        string Outcome,
        string? FinalPath,
        long Bytes,
        string? Sha256,
        bool Verified,
        string? Error);

    private sealed record JsonWarning(string Message, string? Path, string Severity);

    private static string BuildJson(BackupResult result)
    {
        var report = new JsonReport(
            Schema: "nanovault-backup-report/1",
            AppVersion: AppVersion,
            StartedAt: result.StartedAt,
            FinishedAt: result.FinishedAt,
            Device: result.DeviceName,
            Destination: result.DestinationRoot,
            FinalState: result.FinalState.ToString(),
            Totals: new JsonTotals(
                result.TrackResults.Count,
                result.CopiedCount,
                result.SkippedDuplicateCount,
                result.FailedCount,
                result.NotAttemptedCount,
                result.TotalCopiedBytes,
                Math.Round(result.Elapsed.TotalSeconds, 1)),
            Tracks: result.TrackResults.Select(r => new JsonTrack(
                r.Track.RelativeSourcePath,
                r.Outcome.ToString(),
                r.FinalRelativePath,
                r.BytesCopied,
                r.Sha256,
                r.Verified,
                r.Error)).ToList(),
            Warnings: result.Warnings.Select(w => new JsonWarning(w.Message, w.TrackPath, w.Severity.ToString())).ToList(),
            MasterPlaylist: result.MasterPlaylistRelativePath,
            Playlists: result.PlaylistRelativePaths);

        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    // ---------------------------------------------------------------- HTML

    private static string BuildHtml(BackupResult result)
    {
        var e = HtmlEncoder.Default;
        var sb = new StringBuilder(64 * 1024);

        var stateLabel = result.FinalState switch
        {
            BackupState.Completed when result.FailedCount == 0 => "Backup complete",
            BackupState.Completed => "Backup complete with warnings",
            BackupState.Cancelled => "Backup cancelled — completed tracks were kept",
            BackupState.Interrupted => "Backup interrupted — reconnect the iPod to resume",
            _ => "Backup finished",
        };

        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>NanoVault Backup Report</title>
            <style>
              :root { color-scheme: light dark; }
              body { font-family: "Segoe UI", system-ui, sans-serif; margin: 0; padding: 2rem;
                     background: #f5f4fa; color: #1c1b22; }
              @media (prefers-color-scheme: dark) { body { background: #17161d; color: #e8e6f0; } }
              .card { background: rgba(255,255,255,.75); border-radius: 14px; padding: 1.5rem;
                      margin-bottom: 1.5rem; box-shadow: 0 2px 12px rgba(80,70,160,.08); }
              @media (prefers-color-scheme: dark) { .card { background: rgba(40,38,52,.8); } }
              h1 { font-size: 1.5rem; margin: 0 0 .25rem; }
              h1 .accent { color: #6f5bd6; }
              .sub { opacity: .75; margin: 0 0 1rem; }
              .totals { display: flex; flex-wrap: wrap; gap: 1.25rem 2.5rem; }
              .totals div b { display: block; font-size: 1.35rem; }
              table { width: 100%; border-collapse: collapse; font-size: .85rem; }
              th, td { text-align: left; padding: .45rem .6rem; border-bottom: 1px solid rgba(128,128,160,.25);
                       vertical-align: top; word-break: break-word; }
              th { position: sticky; top: 0; background: inherit; }
              .ok { color: #2c8a4b; } .skip { color: #8a7a2c; } .fail { color: #c04545; }
              .hash { font-family: ui-monospace, monospace; font-size: .7rem; opacity: .7; }
              footer { opacity: .6; font-size: .8rem; margin-top: 2rem; }
            </style>
            </head>
            <body>
            """);

        sb.Append("<div class=\"card\"><h1><span class=\"accent\">NanoVault</span> Backup Report</h1>");
        sb.Append("<p class=\"sub\">").Append(e.Encode(stateLabel)).Append(" · Your iPod has not been changed.</p>");
        sb.Append("<div class=\"totals\">");
        AppendTotal(sb, result.CopiedCount.ToString(), "tracks copied");
        AppendTotal(sb, result.SkippedDuplicateCount.ToString(), "duplicates skipped");
        AppendTotal(sb, result.FailedCount.ToString(), "failed");
        AppendTotal(sb, ByteFormatter.FormatSize(result.TotalCopiedBytes), "copied");
        AppendTotal(sb, ByteFormatter.FormatDuration(result.Elapsed), "elapsed");
        sb.Append("</div></div>");

        sb.Append("<div class=\"card\"><table><tbody>");
        AppendRow(sb, e, "Backup date", result.FinishedAt.ToLocalTime().ToString("f"));
        AppendRow(sb, e, "Application version", AppVersion);
        AppendRow(sb, e, "Device", result.DeviceName);
        AppendRow(sb, e, "Destination", result.DestinationRoot);
        if (result.MasterPlaylistRelativePath is not null)
        {
            AppendRow(sb, e, "Playlist", result.MasterPlaylistRelativePath);
        }

        sb.Append("</tbody></table></div>");

        if (result.Warnings.Count > 0)
        {
            sb.Append("<div class=\"card\"><h1>Warnings</h1><table><tbody>");
            foreach (var warning in result.Warnings)
            {
                sb.Append("<tr><td>").Append(e.Encode(warning.Message)).Append("</td><td>")
                  .Append(e.Encode(warning.TrackPath ?? string.Empty)).Append("</td></tr>");
            }

            sb.Append("</tbody></table></div>");
        }

        sb.Append("""
            <div class="card"><h1>Tracks</h1>
            <table><thead><tr><th>Result</th><th>Saved as</th><th>Source file</th><th>SHA-256</th></tr></thead><tbody>
            """);

        foreach (var track in result.TrackResults)
        {
            var (label, cls) = track.Outcome switch
            {
                TrackOutcome.Copied => (track.Verified ? "Copied ✓ verified" : "Copied", "ok"),
                TrackOutcome.SkippedDuplicate => ("Skipped (already backed up)", "skip"),
                TrackOutcome.SkippedProtected => ("Skipped (protected)", "skip"),
                TrackOutcome.SkippedUnreadable => ("Not copied (unreadable)", "fail"),
                TrackOutcome.Failed => ("Failed", "fail"),
                _ => ("Not attempted", "skip"),
            };

            sb.Append("<tr><td class=\"").Append(cls).Append("\">").Append(e.Encode(label)).Append("</td><td>")
              .Append(e.Encode(track.FinalRelativePath ?? "—")).Append("</td><td>")
              .Append(e.Encode(track.Track.RelativeSourcePath)).Append("</td><td class=\"hash\">")
              .Append(e.Encode(track.Sha256 ?? string.Empty)).Append("</td></tr>");
        }

        sb.Append("</tbody></table></div>");
        sb.Append("<footer>Generated by NanoVault ").Append(AppVersion)
          .Append(". NanoVault reads your iPod but never changes it. Protected files are copied as-is and never decrypted.</footer>");
        sb.Append("</body></html>");

        return sb.ToString();
    }

    private static void AppendTotal(StringBuilder sb, string value, string label) =>
        sb.Append("<div><b>").Append(HtmlEncoder.Default.Encode(value)).Append("</b>")
          .Append(HtmlEncoder.Default.Encode(label)).Append("</div>");

    private static void AppendRow(StringBuilder sb, HtmlEncoder e, string key, string value) =>
        sb.Append("<tr><th scope=\"row\">").Append(e.Encode(key)).Append("</th><td>")
          .Append(e.Encode(value)).Append("</td></tr>");
}
