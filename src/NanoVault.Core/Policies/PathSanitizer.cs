using System.Text;

namespace NanoVault.Core.Policies;

/// <summary>
/// Turns metadata strings into safe, readable Windows file and folder names.
/// Prefers readable substitutions over stripping, never produces reserved
/// names, and never leaves trailing periods or spaces.
/// </summary>
public static class PathSanitizer
{
    public const int DefaultMaxComponentLength = 100;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Sanitises a single folder or file-stem component. Returns
    /// <paramref name="fallback"/> when nothing readable remains.
    /// </summary>
    public static string SanitizeComponent(
        string? raw,
        string fallback,
        int maxLength = DefaultMaxComponentLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '<': builder.Append('('); break;
                case '>': builder.Append(')'); break;
                case ':': builder.Append('-'); break;
                case '"': builder.Append('\''); break;
                case '/': builder.Append('-'); break;
                case '\\': builder.Append('-'); break;
                case '|': builder.Append('-'); break;
                case '?': break;
                case '*': break;
                default:
                    builder.Append(char.IsControl(ch) ? ' ' : ch);
                    break;
            }
        }

        var text = CollapseWhitespace(builder.ToString()).Trim();
        text = text.TrimEnd('.', ' ');

        if (text.Length == 0)
        {
            return fallback;
        }

        if (text.Length > maxLength)
        {
            text = text[..maxLength].TrimEnd('.', ' ', '-');
            if (text.Length == 0)
            {
                return fallback;
            }
        }

        if (IsReservedName(text))
        {
            text += " _";
        }

        return text;
    }

    /// <summary>
    /// Sanitises a full file name while preserving the (already safe) extension.
    /// </summary>
    public static string SanitizeFileName(
        string? stem,
        string extension,
        string fallbackStem,
        int maxLength = DefaultMaxComponentLength)
    {
        var safeExtension = NormalizeExtension(extension);
        var maxStem = Math.Max(1, maxLength - safeExtension.Length);
        var safeStem = SanitizeComponent(stem, fallbackStem, maxStem);
        return safeStem + safeExtension;
    }

    /// <summary>Lower-case extension starting with a dot; invalid characters removed.</summary>
    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim().TrimStart('.');
        var builder = new StringBuilder(trimmed.Length + 1);
        builder.Append('.');
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.Length > 1 ? builder.ToString() : string.Empty;
    }

    public static bool IsReservedName(string name)
    {
        var stem = name;
        var dot = name.IndexOf('.');
        if (dot >= 0)
        {
            stem = name[..dot];
        }

        return ReservedNames.Contains(stem.TrimEnd(' '));
    }

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
        }

        return builder.ToString();
    }
}
