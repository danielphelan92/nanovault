using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NanoVault.Core.Models;

namespace NanoVault.App.Converters;

/// <summary>Visible when the bound value equals the converter parameter (by name).</summary>
public sealed class EqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when non-null and (for strings) non-empty.</summary>
public sealed class NotNullVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value is string s && string.IsNullOrWhiteSpace(s)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverts a boolean.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;
}

/// <summary>Formats byte counts like "17.6 GB".</summary>
public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long bytes ? Core.Utilities.ByteFormatter.FormatSize(bytes) : "0 B";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Friendly display names for settings enums.</summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        OrganizationTemplate.ArtistAlbum => "Artist \\ Album \\ 01 - Track Title",
        OrganizationTemplate.AlbumArtistYearAlbum => "Album Artist \\ Year - Album \\ 01 - Track Title",
        OrganizationTemplate.FlatAllMusic => "All Music \\ Artist - Track Title",
        DuplicateBehavior.SkipExactDuplicates => "Skip exact duplicates (recommended)",
        DuplicateBehavior.KeepBoth => "Keep both — add (2), (3), …",
        DuplicateBehavior.ReplaceDestination => "Replace the file in the backup folder",
        AppTheme.System => "Match Windows",
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
