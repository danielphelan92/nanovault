using NanoVault.Core.Abstractions;

namespace NanoVault.Infrastructure.FileSystem;

/// <summary>
/// Real filesystem access. Reads use permissive sharing so a device that is
/// also open in another program can still be backed up; writes are used only
/// for the destination folder, never the iPod.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    private const int ReadBufferSize = 128 * 1024;
    private const int WriteBufferSize = 128 * 1024;

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string root, bool recursive)
    {
        if (!recursive)
        {
            return Directory.EnumerateFiles(root);
        }

        // Tolerant recursion: an unreadable subfolder skips that folder only.
        return Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0, // iPod music files are hidden; include them.
        });
    }

    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path, "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        });

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetCreationTimeUtc(string path) => File.GetCreationTimeUtc(path);

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public Stream OpenRead(string path) =>
        new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            ReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public Stream CreateWrite(string path) =>
        new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            WriteBufferSize,
            FileOptions.Asynchronous);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string path) => File.Delete(path);

    public void SetTimestamps(string path, DateTime createdUtc, DateTime modifiedUtc)
    {
        if (createdUtc > DateTime.MinValue)
        {
            File.SetCreationTimeUtc(path, createdUtc);
        }

        if (modifiedUtc > DateTime.MinValue)
        {
            File.SetLastWriteTimeUtc(path, modifiedUtc);
        }
    }

    public long GetAvailableFreeSpace(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return long.MaxValue;
        }

        return new DriveInfo(root).AvailableFreeSpace;
    }
}
