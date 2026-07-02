using System.Security.Cryptography;
using NanoVault.Core.Abstractions;

namespace NanoVault.Infrastructure.Verification;

/// <summary>Streaming SHA-256 hashing with cancellation support.</summary>
public sealed class Sha256FileVerificationService : IFileVerificationService
{
    private const int BufferSize = 1024 * 1024;

    private readonly IReadOnlyFileSystem _fileSystem;

    public Sha256FileVerificationService(IReadOnlyFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = _fileSystem.OpenRead(path);
        return await ComputeSha256Async(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }
}
