namespace NanoVault.Core.Policies;

/// <summary>Which file types NanoVault discovers and copies (never transcodes).</summary>
public static class SupportedMedia
{
    /// <summary>Ordinary, unprotected audio formats.</summary>
    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".m4b", ".aac", ".wav", ".aiff", ".aif", ".aa", ".alac",
    };

    /// <summary>Formats that are likely DRM-protected. Copied as-is, never decrypted.</summary>
    public static readonly IReadOnlySet<string> ProtectedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".m4p",
    };

    public static bool IsSupported(string extension) =>
        AudioExtensions.Contains(extension) || ProtectedExtensions.Contains(extension);

    public static bool IsLikelyProtected(string extension) => ProtectedExtensions.Contains(extension);

    /// <summary>Friendly format label from an extension, e.g. ".mp3" → "MP3".</summary>
    public static string FormatLabel(string extension) => extension.TrimStart('.').ToUpperInvariant() switch
    {
        "M4A" => "AAC (M4A)",
        "M4B" => "Audiobook (M4B)",
        "M4P" => "Protected AAC (M4P)",
        "AA" => "Audible (AA)",
        "AIF" or "AIFF" => "AIFF",
        var other => other,
    };
}
