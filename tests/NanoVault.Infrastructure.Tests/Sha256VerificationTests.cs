using System.Security.Cryptography;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Verification;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class Sha256VerificationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly Sha256FileVerificationService _service = new(new PhysicalFileSystem());

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Hash_matches_dotnet_reference_implementation()
    {
        var data = new byte[100_000];
        Random.Shared.NextBytes(data);
        var path = _workspace.WriteFile("file.bin", data);

        var expected = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var actual = await _service.ComputeSha256Async(path);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Identical_files_hash_identically_and_different_files_differ()
    {
        var a = _workspace.WriteFile("a.bin", "same content");
        var b = _workspace.WriteFile("b.bin", "same content");
        var c = _workspace.WriteFile("c.bin", "different content");

        var hashA = await _service.ComputeSha256Async(a);
        var hashB = await _service.ComputeSha256Async(b);
        var hashC = await _service.ComputeSha256Async(c);

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(hashA, hashC);
    }

    [Fact]
    public async Task Hashing_supports_cancellation()
    {
        var data = new byte[64 * 1024 * 1024];
        var path = _workspace.WriteFile("big.bin", data);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ComputeSha256Async(path, cts.Token));
    }

    [Fact]
    public async Task Empty_file_hashes_to_the_known_empty_digest()
    {
        var path = _workspace.WriteFile("empty.bin", Array.Empty<byte>());
        var hash = await _service.ComputeSha256Async(path);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }
}
