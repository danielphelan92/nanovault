using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.Infrastructure.Backup;

/// <summary>
/// Copies one planned track safely:
/// stream to "&lt;name&gt;.nanovault-partial", hash while copying, optionally verify
/// the written bytes with SHA-256, preserve timestamps, then atomically rename.
/// Transient destination errors retry a few times; a damaged source never
/// retries endlessly; the iPod is only ever opened for reading.
/// </summary>
public sealed class TrackCopyService : ITrackCopyService
{
    public const string PartialSuffix = ".nanovault-partial";
    private const int MaxDestinationAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(750);

    private readonly IFileSystem _fileSystem;
    private readonly IFileVerificationService _verification;
    private readonly ILogger<TrackCopyService> _logger;

    public TrackCopyService(
        IFileSystem fileSystem,
        IFileVerificationService verification,
        ILogger<TrackCopyService> logger)
    {
        _fileSystem = fileSystem;
        _verification = verification;
        _logger = logger;
    }

    public async Task<TrackBackupResult> CopyTrackAsync(
        BackupPlanItem item,
        string destinationRoot,
        CopyContext context,
        CancellationToken cancellationToken = default)
    {
        if (!item.WillCopy || item.DestinationRelativePath is null)
        {
            return new TrackBackupResult
            {
                Track = item.Track,
                Outcome = item.Action switch
                {
                    PlanItemAction.SkipExactDuplicate => TrackOutcome.SkippedDuplicate,
                    PlanItemAction.SkipProtectedExcluded => TrackOutcome.SkippedProtected,
                    PlanItemAction.SkipUnreadable => TrackOutcome.SkippedUnreadable,
                    _ => TrackOutcome.NotAttempted,
                },
                FinalRelativePath = item.DestinationRelativePath,
            };
        }

        var relativePath = item.DestinationRelativePath;
        var destination = Path.Combine(destinationRoot, relativePath);
        var partial = destination + PartialSuffix;

        var attempt = 0;
        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();
            await context.Pause.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await CopyOnceAsync(item, destinationRoot, relativePath, destination, partial, context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(partial);
                throw;
            }
            catch (SourceReadException ex)
            {
                // Damaged or vanished source: report, never retry endlessly.
                TryDeletePartial(partial);
                _logger.LogWarning(ex.InnerException, "Source unreadable: {Path}", item.Track.SourcePath);
                return Failed(item, "This track could not be read from the iPod.", ex.InnerException?.ToString());
            }
            catch (Exception ex) when (IsTransientDestinationError(ex) && attempt < MaxDestinationAttempts)
            {
                // Antivirus locks and similar transient destination problems.
                _logger.LogWarning(ex, "Destination error copying {Path} (attempt {Attempt}); retrying", relativePath, attempt);
                TryDeletePartial(partial);
                await Task.Delay(RetryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                TryDeletePartial(partial);
                _logger.LogError(ex, "Failed to copy {Path}", relativePath);
                return Failed(item, "This track could not be copied to the backup folder.", ex.ToString());
            }
        }
    }

    private async Task<TrackBackupResult> CopyOnceAsync(
        BackupPlanItem item,
        string destinationRoot,
        string relativePath,
        string destination,
        string partial,
        CopyContext context,
        CancellationToken cancellationToken)
    {
        var track = item.Track;
        var options = context.Options;

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        // Resume support: a file that already exists and matches the source
        // (size, and hash when verification is on) was finished by an earlier
        // run — count it instead of copying again.
        if (item.Action != PlanItemAction.ReplaceExisting && _fileSystem.FileExists(destination))
        {
            if (await MatchesSourceAsync(track, destination, options.VerifyCopies, cancellationToken).ConfigureAwait(false))
            {
                return new TrackBackupResult
                {
                    Track = track,
                    Outcome = TrackOutcome.SkippedDuplicate,
                    FinalRelativePath = relativePath,
                    Verified = options.VerifyCopies,
                };
            }

            // A different file appeared here since planning; never overwrite it.
            relativePath = FindAlternateName(destinationRoot, relativePath);
            destination = Path.Combine(destinationRoot, relativePath);
            partial = destination + PartialSuffix;
        }

        long bytesCopied = 0;
        string sourceHash;

        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            try
            {
                Stream source;
                try
                {
                    source = _fileSystem.OpenRead(track.SourcePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new SourceReadException(ex);
                }

                await using (source.ConfigureAwait(false))
                {
                    var destinationStream = _fileSystem.CreateWrite(partial);
                    await using (destinationStream.ConfigureAwait(false))
                    {
                        var buffer = new byte[Math.Clamp(options.CopyBufferBytes, 64 * 1024, 8 * 1024 * 1024)];

                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await context.Pause.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);

                            int read;
                            try
                            {
                                read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                            {
                                throw new SourceReadException(ex);
                            }

                            if (read == 0)
                            {
                                break;
                            }

                            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            sha.AppendData(buffer, 0, read);
                            bytesCopied += read;
                            context.BytesProgress?.Report(read);
                        }

                        await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // Roll progress back so a retry or failure never double-counts.
                if (bytesCopied > 0)
                {
                    context.BytesProgress?.Report(-bytesCopied);
                }

                throw;
            }

            sourceHash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        }

        var verified = false;
        if (options.VerifyCopies)
        {
            var writtenSize = _fileSystem.GetFileSize(partial);
            var writtenHash = await _verification.ComputeSha256Async(partial, cancellationToken).ConfigureAwait(false);

            if (writtenSize != bytesCopied || !string.Equals(writtenHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDeletePartial(partial);
                _logger.LogError(
                    "Verification failed for {Path}: wrote {Written} bytes, hash mismatch={Mismatch}",
                    relativePath, writtenSize, writtenHash != sourceHash);
                return Failed(item, "The copied file did not verify correctly and was removed. The original on the iPod is untouched.",
                    $"size {bytesCopied} vs {writtenSize}; sha256 {sourceHash} vs {writtenHash}");
            }

            verified = true;
        }

        if (options.PreserveTimestamps)
        {
            try
            {
                _fileSystem.SetTimestamps(partial, track.CreatedUtc, track.ModifiedUtc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Could not preserve timestamps for {Path}", relativePath);
            }
        }

        _fileSystem.MoveFile(partial, destination, overwrite: item.Action == PlanItemAction.ReplaceExisting);

        return new TrackBackupResult
        {
            Track = track,
            Outcome = TrackOutcome.Copied,
            FinalRelativePath = relativePath,
            BytesCopied = bytesCopied,
            Sha256 = sourceHash,
            Verified = verified,
        };
    }

    private async Task<bool> MatchesSourceAsync(
        DiscoveredTrack track,
        string destination,
        bool compareHashes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_fileSystem.GetFileSize(destination) != track.SizeBytes)
            {
                return false;
            }

            if (!compareHashes)
            {
                return true;
            }

            var sourceHash = await _verification.ComputeSha256Async(track.SourcePath, cancellationToken).ConfigureAwait(false);
            var destinationHash = await _verification.ComputeSha256Async(destination, cancellationToken).ConfigureAwait(false);
            return string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>First free "name (2).ext" style path under the destination root.</summary>
    private string FindAlternateName(string destinationRoot, string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var extension = Path.GetExtension(relativePath);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!_fileSystem.FileExists(Path.Combine(destinationRoot, candidate)))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }

    private static TrackBackupResult Failed(BackupPlanItem item, string message, string? technicalDetail) => new()
    {
        Track = item.Track,
        Outcome = TrackOutcome.Failed,
        FinalRelativePath = item.DestinationRelativePath,
        Error = message,
        TechnicalDetail = technicalDetail,
    };

    private void TryDeletePartial(string partial)
    {
        try
        {
            if (_fileSystem.FileExists(partial))
            {
                _fileSystem.DeleteFile(partial);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not remove partial file {Partial}", partial);
        }
    }

    private static bool IsTransientDestinationError(Exception ex) =>
        ex is IOException io && io is not FileNotFoundException and not DirectoryNotFoundException;

    /// <summary>Wraps source-side read failures so they are never retried like destination errors.</summary>
    private sealed class SourceReadException : Exception
    {
        public SourceReadException(Exception inner) : base("Source read failed", inner)
        {
        }
    }
}
