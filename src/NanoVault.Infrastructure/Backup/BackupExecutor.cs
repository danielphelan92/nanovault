using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Threading;

namespace NanoVault.Infrastructure.Backup;

/// <summary>
/// Runs every item in a plan through the copy service with live progress,
/// pause and cancel support, and disconnect detection. A failed track never
/// stops the rest of the backup; a vanished device turns the run into a
/// recoverable "interrupted" state with all completed files kept.
/// </summary>
public sealed class BackupExecutor : IBackupExecutor
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    private readonly ITrackCopyService _copyService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BackupExecutor> _logger;

    public BackupExecutor(
        ITrackCopyService copyService,
        IFileSystem fileSystem,
        ILogger<BackupExecutor> logger)
    {
        _copyService = copyService;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<BackupResult> ExecuteAsync(
        BackupPlan plan,
        IProgress<BackupProgress>? progress = null,
        PauseToken pause = default,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        _fileSystem.CreateDirectory(plan.DestinationRoot);
        CleanAbandonedPartials(plan.DestinationRoot);

        var items = plan.Items;
        var results = new TrackBackupResult?[items.Count];

        var totalCopyTracks = plan.CopyCount;
        var totalBytes = plan.BytesToCopy;
        long copiedBytes = 0;
        var completedCopyTracks = 0;
        string? currentTrack = null;
        var gate = new object();

        var stopwatch = Stopwatch.StartNew();
        var speedometer = new Speedometer(stopwatch);
        var lastReport = TimeSpan.MinValue;

        void Notify(bool force)
        {
            if (progress is null)
            {
                return;
            }

            lock (gate)
            {
                if (!force && stopwatch.Elapsed - lastReport < ProgressInterval)
                {
                    return;
                }

                lastReport = stopwatch.Elapsed;
                progress.Report(new BackupProgress
                {
                    State = pause.IsPaused ? BackupState.Paused : BackupState.Copying,
                    TotalTracks = totalCopyTracks,
                    CompletedTracks = Volatile.Read(ref completedCopyTracks),
                    TotalBytes = totalBytes,
                    CopiedBytes = Volatile.Read(ref copiedBytes),
                    CurrentTrack = currentTrack,
                    BytesPerSecond = speedometer.BytesPerSecond,
                });
            }
        }

        var bytesProgress = new SynchronousProgress<long>(delta =>
        {
            Interlocked.Add(ref copiedBytes, delta);
            speedometer.Add(delta);
            Notify(force: false);
        });

        var context = new CopyContext
        {
            Options = plan.Options,
            Pause = pause,
            BytesProgress = bytesProgress,
        };

        var interrupted = false;
        using var internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var concurrency = Math.Clamp(plan.Options.MaxConcurrentCopies, 1, 2);

        Notify(force: true);

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, items.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = concurrency,
                    CancellationToken = internalCts.Token,
                },
                async (index, token) =>
                {
                    var item = items[index];
                    if (item.WillCopy)
                    {
                        currentTrack = item.Track.DisplayName;
                        Notify(force: true);
                    }

                    var result = await _copyService
                        .CopyTrackAsync(item, plan.DestinationRoot, context, token)
                        .ConfigureAwait(false);

                    results[index] = result;

                    if (item.WillCopy)
                    {
                        Interlocked.Increment(ref completedCopyTracks);
                        Notify(force: true);
                    }

                    // If a copy failed because the whole device vanished, stop
                    // cleanly instead of failing every remaining track.
                    if (result.Outcome == TrackOutcome.Failed
                        && !_fileSystem.DirectoryExists(plan.Device.RootPath))
                    {
                        _logger.LogWarning("iPod {Device} disconnected during backup", plan.Device.DisplayName);
                        interrupted = true;
                        internalCts.Cancel();
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (interrupted || cancellationToken.IsCancellationRequested)
        {
            // Expected: user cancel or device disconnect. Completed files are kept.
        }

        // Anything not processed keeps its skip classification, or NotAttempted.
        for (var i = 0; i < items.Count; i++)
        {
            results[i] ??= new TrackBackupResult
            {
                Track = items[i].Track,
                Outcome = TrackOutcome.NotAttempted,
                FinalRelativePath = items[i].DestinationRelativePath,
            };
        }

        var finalResults = results.Select(r => r!).ToList();
        var warnings = BuildWarnings(plan, finalResults);

        var finalState = interrupted
            ? BackupState.Interrupted
            : cancellationToken.IsCancellationRequested
                ? BackupState.Cancelled
                : BackupState.Completed;

        var result = new BackupResult
        {
            DestinationRoot = plan.DestinationRoot,
            DeviceName = plan.Device.DisplayName,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now,
            FinalState = finalState,
            TrackResults = finalResults,
            Warnings = warnings,
        };

        _logger.LogInformation(
            "Backup {State}: {Copied} copied, {Skipped} duplicates skipped, {Failed} failed in {Elapsed}",
            finalState, result.CopiedCount, result.SkippedDuplicateCount, result.FailedCount, result.Elapsed);

        return result;
    }

    private static IReadOnlyList<BackupWarning> BuildWarnings(BackupPlan plan, IReadOnlyList<TrackBackupResult> results)
    {
        var warnings = new List<BackupWarning>();

        foreach (var scanWarning in plan.Warnings)
        {
            warnings.Add(new BackupWarning(scanWarning.Message, scanWarning.Path, scanWarning.Severity));
        }

        foreach (var result in results)
        {
            switch (result.Outcome)
            {
                case TrackOutcome.Failed:
                    warnings.Add(new BackupWarning(
                        result.Error ?? "This track could not be copied.",
                        result.Track.SourcePath,
                        WarningSeverity.Error));
                    break;
                case TrackOutcome.SkippedUnreadable:
                    warnings.Add(new BackupWarning(
                        "This track could not be read and was not copied.",
                        result.Track.SourcePath,
                        WarningSeverity.Warning));
                    break;
                case TrackOutcome.SkippedProtected when result.FinalRelativePath is null:
                    warnings.Add(new BackupWarning(
                        "A protected file was excluded by settings.",
                        result.Track.SourcePath,
                        WarningSeverity.Info));
                    break;
            }
        }

        return warnings;
    }

    private void CleanAbandonedPartials(string destinationRoot)
    {
        try
        {
            if (!_fileSystem.DirectoryExists(destinationRoot))
            {
                return;
            }

            foreach (var file in _fileSystem.EnumerateFiles(destinationRoot, recursive: true))
            {
                if (file.EndsWith(TrackCopyService.PartialSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        _fileSystem.DeleteFile(file);
                        _logger.LogInformation("Removed abandoned partial file {File}", file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.LogDebug(ex, "Could not remove partial file {File}", file);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Partial-file cleanup skipped");
        }
    }

    /// <summary>IProgress that invokes synchronously (no sync-context posting).</summary>
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    /// <summary>Thread-safe sliding-window transfer speed over the last five seconds.</summary>
    private sealed class Speedometer
    {
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);
        private readonly Stopwatch _stopwatch;
        private readonly Queue<(TimeSpan At, long Bytes)> _samples = new();
        private readonly object _gate = new();
        private long _windowBytes;

        public Speedometer(Stopwatch stopwatch) => _stopwatch = stopwatch;

        public void Add(long bytes)
        {
            lock (_gate)
            {
                var now = _stopwatch.Elapsed;
                _samples.Enqueue((now, bytes));
                _windowBytes += bytes;
                Prune(now);
            }
        }

        public double BytesPerSecond
        {
            get
            {
                lock (_gate)
                {
                    var now = _stopwatch.Elapsed;
                    Prune(now);
                    if (_samples.Count == 0)
                    {
                        return 0;
                    }

                    var span = now - _samples.Peek().At;
                    var seconds = Math.Max(span.TotalSeconds, 0.5);
                    return Math.Max(0, _windowBytes) / seconds;
                }
            }
        }

        private void Prune(TimeSpan now)
        {
            while (_samples.Count > 0 && now - _samples.Peek().At > Window)
            {
                _windowBytes -= _samples.Dequeue().Bytes;
            }
        }
    }
}
