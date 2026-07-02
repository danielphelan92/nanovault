namespace NanoVault.Core.Policies;

/// <summary>
/// The iPod database stores media locations with colon separators, for
/// example ":iPod_Control:Music:F00:ABCD.mp3". This converts between that
/// form, local relative paths, and a canonical comparison key.
/// </summary>
public static class IpodPathNormalizer
{
    /// <summary>
    /// Converts an iPod-internal location to a relative path using the local
    /// directory separator. Returns null for empty or clearly invalid input.
    /// </summary>
    public static string? ToRelativePath(string? ipodInternalPath)
    {
        if (string.IsNullOrWhiteSpace(ipodInternalPath))
        {
            return null;
        }

        var parts = ipodInternalPath
            .Replace('\\', ':')
            .Replace('/', ':')
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return null;
        }

        // Reject traversal segments outright; the database should never contain them.
        if (parts.Any(p => p is "." or ".."))
        {
            return null;
        }

        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    /// <summary>
    /// Canonical key for comparing paths from any layer: forward slashes,
    /// no leading separator, ordinal lower-case.
    /// </summary>
    public static string ToComparableKey(string path)
    {
        var normalized = path
            .Replace('\\', '/')
            .Replace(':', '/')
            .Trim()
            .Trim('/');

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.ToLowerInvariant();
    }
}
