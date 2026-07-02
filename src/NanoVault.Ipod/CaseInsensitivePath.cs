using NanoVault.Core.Abstractions;

namespace NanoVault.Ipod;

/// <summary>
/// Case-insensitive lookups for well-known iPod folders, so detection works
/// no matter how the volume reports "iPod_Control".
/// </summary>
public static class CaseInsensitivePath
{
    /// <summary>Finds a direct child directory by name, ignoring case. Null when absent.</summary>
    public static string? FindDirectory(IReadOnlyFileSystem fileSystem, string parent, string name)
    {
        if (!fileSystem.DirectoryExists(parent))
        {
            return null;
        }

        // Fast path: most filesystems resolve this directly.
        var direct = Path.Combine(parent, name);
        if (fileSystem.DirectoryExists(direct))
        {
            return direct;
        }

        try
        {
            foreach (var child in fileSystem.EnumerateDirectories(parent))
            {
                if (string.Equals(Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    /// <summary>Finds a file inside a directory by name, ignoring case. Null when absent.</summary>
    public static string? FindFile(IReadOnlyFileSystem fileSystem, string directory, string fileName)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return null;
        }

        var direct = Path.Combine(directory, fileName);
        if (fileSystem.FileExists(direct))
        {
            return direct;
        }

        try
        {
            foreach (var file in fileSystem.EnumerateFiles(directory, recursive: false))
            {
                if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }
}
