using System.Globalization;

namespace NanoVault.Core.Utilities;

/// <summary>Human-friendly sizes, speeds, and durations for the UI and reports.</summary>
public static class ByteFormatter
{
    public static string FormatSize(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 || unit == 0 ? "0" : "0.#";
        return value.ToString(format, CultureInfo.InvariantCulture) + " " + units[unit];
    }

    public static string FormatSpeed(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "—" : FormatSize((long)bytesPerSecond) + "/s";

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return duration.TotalMinutes >= 1
            ? $"{duration.Minutes}m {duration.Seconds}s"
            : $"{Math.Max(0, duration.Seconds)}s";
    }
}
