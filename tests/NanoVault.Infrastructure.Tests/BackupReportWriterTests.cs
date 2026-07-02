using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Reports;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class BackupReportWriterTests : IDisposable
{
    private readonly TempWorkspace _destination = new();
    private readonly BackupReportWriter _writer =
        new(new PhysicalFileSystem(), NullLogger<BackupReportWriter>.Instance);

    public void Dispose() => _destination.Dispose();

    private BackupResult SampleResult()
    {
        var startedAt = DateTimeOffset.Now.AddMinutes(-3);
        return new BackupResult
        {
            DestinationRoot = _destination.Root,
            DeviceName = "Danny's iPod",
            StartedAt = startedAt,
            FinishedAt = startedAt.AddMinutes(3),
            FinalState = BackupState.Completed,
            TrackResults =
            [
                new TrackBackupResult
                {
                    Track = new DiscoveredTrack
                    {
                        SourcePath = "/ipod/iPod_Control/Music/F00/A.mp3",
                        RelativeSourcePath = "iPod_Control/Music/F00/A.mp3",
                        SizeBytes = 111,
                        Extension = ".mp3",
                        Metadata = new TrackMetadata { Title = "Song <One>", Artist = "A&B" },
                    },
                    Outcome = TrackOutcome.Copied,
                    FinalRelativePath = "A&B/Album/01 - Song.mp3",
                    BytesCopied = 111,
                    Sha256 = "abc123",
                    Verified = true,
                },
                new TrackBackupResult
                {
                    Track = new DiscoveredTrack
                    {
                        SourcePath = "/ipod/iPod_Control/Music/F00/B.mp3",
                        RelativeSourcePath = "iPod_Control/Music/F00/B.mp3",
                        SizeBytes = 5,
                        Extension = ".mp3",
                    },
                    Outcome = TrackOutcome.Failed,
                    Error = "This track could not be read.",
                },
            ],
            Warnings = [new BackupWarning("One file failed", "/ipod/B.mp3", WarningSeverity.Error)],
            MasterPlaylistRelativePath = "All iPod Music.m3u8",
        };
    }

    [Fact]
    public async Task Writes_html_and_json_reports_into_the_backup_root()
    {
        var paths = await _writer.WriteAsync(SampleResult());

        Assert.NotNull(paths.HtmlPath);
        Assert.NotNull(paths.JsonPath);
        Assert.StartsWith(_destination.Root, paths.HtmlPath!);
        Assert.Contains("NanoVault Backup Report", Path.GetFileName(paths.HtmlPath!));

        var html = File.ReadAllText(paths.HtmlPath!);
        Assert.Contains("Danny&#x27;s iPod", html);
        Assert.Contains("A&amp;B/Album/01 - Song.mp3", html); // paths are HTML-encoded
        Assert.Contains("abc123", html);
        Assert.Contains("Your iPod has not been changed", html);
    }

    [Fact]
    public async Task Json_report_is_machine_readable_with_totals_and_tracks()
    {
        var paths = await _writer.WriteAsync(SampleResult());

        using var json = JsonDocument.Parse(File.ReadAllText(paths.JsonPath!));
        var root = json.RootElement;

        Assert.Equal("nanovault-backup-report/1", root.GetProperty("schema").GetString());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("copied").GetInt32());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("failed").GetInt32());
        Assert.Equal(2, root.GetProperty("tracks").GetArrayLength());
        Assert.Equal("Copied", root.GetProperty("tracks")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Second_report_on_the_same_day_gets_a_distinct_name()
    {
        var first = await _writer.WriteAsync(SampleResult());
        var second = await _writer.WriteAsync(SampleResult());

        Assert.NotEqual(first.HtmlPath, second.HtmlPath);
        Assert.True(File.Exists(first.HtmlPath!));
        Assert.True(File.Exists(second.HtmlPath!));
    }
}
