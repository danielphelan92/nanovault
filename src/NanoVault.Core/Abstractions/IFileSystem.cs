namespace NanoVault.Core.Abstractions;

/// <summary>
/// Read-only filesystem view. Everything that touches the iPod depends on
/// this interface only, so the source device can never be modified.
/// </summary>
public interface IReadOnlyFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);

    /// <summary>Enumerates files below <paramref name="root"/>. Never throws on individual unreadable entries.</summary>
    IEnumerable<string> EnumerateFiles(string root, bool recursive);

    IEnumerable<string> EnumerateDirectories(string path);

    long GetFileSize(string path);
    DateTime GetCreationTimeUtc(string path);
    DateTime GetLastWriteTimeUtc(string path);

    /// <summary>
    /// Opens for reading with permissive sharing
    /// (FileAccess.Read, FileShare.ReadWrite | FileShare.Delete).
    /// </summary>
    Stream OpenRead(string path);
}

/// <summary>
/// Full filesystem access, used only for the user's chosen destination and
/// for NanoVault's own settings, logs, and reports — never for the iPod.
/// </summary>
public interface IFileSystem : IReadOnlyFileSystem
{
    void CreateDirectory(string path);

    /// <summary>Creates (or truncates) a file for asynchronous writing.</summary>
    Stream CreateWrite(string path);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);
    void DeleteFile(string path);
    void SetTimestamps(string path, DateTime createdUtc, DateTime modifiedUtc);

    /// <summary>Free bytes available to the current user on the volume containing <paramref name="path"/>.</summary>
    long GetAvailableFreeSpace(string path);
}
