using System.Diagnostics;
using System.Windows;
using NanoVault.App.ViewModels;

namespace NanoVault.App.Services;

/// <summary>Folder browser using the modern Windows folder dialog.</summary>
public sealed class WpfFolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose backup folder",
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && System.IO.Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}

/// <summary>Opens folders and files with the Windows shell.</summary>
public sealed class WindowsShellService : IShellService
{
    public void OpenFolder(string path) => Open(path);

    public void OpenFile(string path) => Open(path);

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nothing useful to do; the path is shown in the UI anyway.
        }
    }
}

/// <summary>Marshals callbacks onto the WPF dispatcher.</summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
