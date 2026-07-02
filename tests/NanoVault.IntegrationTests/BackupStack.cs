using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;
using NanoVault.Infrastructure.Backup;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Metadata;
using NanoVault.Infrastructure.Playlists;
using NanoVault.Infrastructure.Reports;
using NanoVault.Infrastructure.Verification;
using NanoVault.Ipod;
using NanoVault.Ipod.Database;

namespace NanoVault.IntegrationTests;

/// <summary>
/// The full production service stack wired exactly like the app's composition
/// root, but over an injectable filesystem so device removal can be simulated.
/// </summary>
public sealed class BackupStack
{
    public BackupStack(IFileSystem? fileSystem = null)
    {
        FileSystem = fileSystem ?? new PhysicalFileSystem();

        Verification = new Sha256FileVerificationService(FileSystem);
        MetadataReader = new TagLibAudioMetadataReader(FileSystem, NullLogger<TagLibAudioMetadataReader>.Instance);
        DatabaseReader = new ITunesDbReader(FileSystem, NullLogger<ITunesDbReader>.Instance);
        Scanner = new IpodScanner(FileSystem, MetadataReader, DatabaseReader, NullLogger<IpodScanner>.Instance);
        DuplicateResolver = new DuplicateResolver(FileSystem, Verification, NullLogger<DuplicateResolver>.Instance);
        Planner = new BackupPlanner(FileSystem, DuplicateResolver, NullLogger<BackupPlanner>.Instance);
        CopyService = new TrackCopyService(FileSystem, Verification, NullLogger<TrackCopyService>.Instance);
        Executor = new BackupExecutor(CopyService, FileSystem, NullLogger<BackupExecutor>.Instance);
        PlaylistWriter = new M3u8PlaylistWriter(FileSystem, NullLogger<M3u8PlaylistWriter>.Instance);
        ReportWriter = new BackupReportWriter(FileSystem, NullLogger<BackupReportWriter>.Instance);
        Service = new BackupService(Executor, PlaylistWriter, ReportWriter, NullLogger<BackupService>.Instance);
    }

    public IFileSystem FileSystem { get; }
    public Sha256FileVerificationService Verification { get; }
    public TagLibAudioMetadataReader MetadataReader { get; }
    public ITunesDbReader DatabaseReader { get; }
    public IpodScanner Scanner { get; }
    public DuplicateResolver DuplicateResolver { get; }
    public BackupPlanner Planner { get; }
    public TrackCopyService CopyService { get; }
    public BackupExecutor Executor { get; }
    public M3u8PlaylistWriter PlaylistWriter { get; }
    public BackupReportWriter ReportWriter { get; }
    public BackupService Service { get; }

    /// <summary>Scan → plan (all tracks) → run, in one call.</summary>
    public async Task<(ScanResult Scan, BackupPlan Plan, BackupResult Result)> BackupEverythingAsync(
        IpodDevice device,
        string destination,
        BackupOptions? options = null,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default)
    {
        var scan = await Scanner.ScanAsync(device, null, cancellationToken);
        var plan = await Planner.CreatePlanAsync(scan, scan.Tracks, destination, options ?? new BackupOptions(), cancellationToken);
        var result = await Service.RunAsync(plan, progress, pause, cancellationToken);
        return (scan, plan, result);
    }
}
