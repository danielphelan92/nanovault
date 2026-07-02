using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.Infrastructure.Backup;

/// <summary>
/// Staged duplicate detection: destination path, then file size, then SHA-256
/// only when both match. Never allows a silent overwrite — when content
/// differs, the copy gets an alternate " (2)" style name unless the user
/// explicitly chose to replace files.
/// </summary>
public sealed class DuplicateResolver : IDuplicateResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileVerificationService _verification;
    private readonly ILogger<DuplicateResolver> _logger;

    public DuplicateResolver(
        IFileSystem fileSystem,
        IFileVerificationService verification,
        ILogger<DuplicateResolver> logger)
    {
        _fileSystem = fileSystem;
        _verification = verification;
        _logger = logger;
    }

    public async Task<DuplicateDecision> ResolveAsync(
        DiscoveredTrack track,
        string destinationRoot,
        string destinationRelativePath,
        DuplicateBehavior behavior,
        CancellationToken cancellationToken = default)
    {
        var destination = Path.Combine(destinationRoot, destinationRelativePath);

        if (!_fileSystem.FileExists(destination))
        {
            return new DuplicateDecision(DuplicateOutcome.NoConflict);
        }

        if (behavior == DuplicateBehavior.ReplaceDestination)
        {
            return new DuplicateDecision(DuplicateOutcome.Replace);
        }

        if (await IsExactDuplicateAsync(track, destination, cancellationToken).ConfigureAwait(false))
        {
            return behavior == DuplicateBehavior.SkipExactDuplicates
                ? new DuplicateDecision(DuplicateOutcome.ExactDuplicate)
                : new DuplicateDecision(DuplicateOutcome.UseAlternateName, FindAlternateName(destinationRoot, destinationRelativePath));
        }

        // Same name, different content: always keep both rather than overwrite.
        return new DuplicateDecision(DuplicateOutcome.UseAlternateName, FindAlternateName(destinationRoot, destinationRelativePath));
    }

    private async Task<bool> IsExactDuplicateAsync(DiscoveredTrack track, string destination, CancellationToken cancellationToken)
    {
        long destinationSize;
        try
        {
            destinationSize = _fileSystem.GetFileSize(destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (destinationSize != track.SizeBytes)
        {
            return false;
        }

        try
        {
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
            _logger.LogWarning(ex, "Could not hash {Destination} for duplicate check; keeping both files", destination);
            return false;
        }
    }

    /// <summary>First free "name (2).ext", "name (3).ext", … under the destination.</summary>
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

        // Practically unreachable; timestamped name as a last resort.
        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }
}
