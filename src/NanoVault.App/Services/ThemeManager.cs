using System.Windows;
using NanoVault.Core.Models;

namespace NanoVault.App.Services;

/// <summary>
/// Swaps the light/dark theme dictionary at runtime. "System" follows the
/// Windows apps theme preference.
/// </summary>
public static class ThemeManager
{
    public static void Apply(AppTheme theme)
    {
        var useDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemPrefersDark(),
        };

        var source = new Uri(
            useDark ? "Themes/Theme.Dark.xaml" : "Themes/Theme.Light.xaml",
            UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeDictionary = dictionaries.FirstOrDefault(d =>
            d.Source is { OriginalString: var s } && s.Contains("Themes/Theme.", StringComparison.OrdinalIgnoreCase));

        var replacement = new ResourceDictionary { Source = source };
        if (themeDictionary is not null)
        {
            var index = dictionaries.IndexOf(themeDictionary);
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }
    }

    private static bool SystemPrefersDark()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException)
        {
            return false;
        }
    }
}
