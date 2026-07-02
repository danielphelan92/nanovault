using NanoVault.Core.Models;

namespace NanoVault.Infrastructure.Tests;

/// <summary>Shared helpers: a disposable temp workspace and track factories.</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    public string WriteFile(string relativePath, byte[] content)
    {
        var full = PathOf(relativePath.Split('/'));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        return full;
    }

    public string WriteFile(string relativePath, string content) =>
        WriteFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public static class Tracks
{
    public static DiscoveredTrack FromFile(string sourceRoot, string fullPath, TrackMetadata? metadata = null)
    {
        var info = new FileInfo(fullPath);
        return new DiscoveredTrack
        {
            SourcePath = fullPath,
            RelativeSourcePath = Path.GetRelativePath(sourceRoot, fullPath),
            SizeBytes = info.Length,
            Extension = info.Extension.ToLowerInvariant(),
            Metadata = metadata ?? TrackMetadata.Empty,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
        };
    }
}
