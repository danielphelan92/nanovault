namespace NanoVault.App.ViewModels;

/// <summary>Which screen the shell is showing.</summary>
public enum AppScreen
{
    Home = 0,
    Tracks = 1,
    Progress = 2,
    Completion = 3,
    Settings = 4,
    Troubleshooting = 5,
}

/// <summary>The six device-card states from the specification.</summary>
public enum DeviceCardState
{
    Searching = 0,
    Found = 1,
    MultipleFound = 2,
    StorageUnavailable = 3,
    NotFound = 4,
    DisconnectedDuringBackup = 5,
}

/// <summary>Folder browser shown from the UI layer; faked in tests.</summary>
public interface IFolderPicker
{
    string? PickFolder(string? initialFolder);
}

/// <summary>Opens folders and files in the platform shell (Explorer).</summary>
public interface IShellService
{
    void OpenFolder(string path);
    void OpenFile(string path);
}

/// <summary>Marshals service callbacks onto the UI thread; immediate in tests.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

/// <summary>Runs callbacks inline — used by tests and safe for headless use.</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
