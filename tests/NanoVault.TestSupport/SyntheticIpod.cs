using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.TestSupport;

/// <summary>
/// A disposable synthetic iPod on the local disk:
///   &lt;root&gt;\iPod_Control\Music\F00, F01 with generated audio files and
///   &lt;root&gt;\iPod_Control\iTunes\iTunesDB written by <see cref="ITunesDbBuilder"/>.
/// </summary>
public sealed class SyntheticIpod : IDisposable
{
    public SyntheticIpod(string? root = null)
    {
        Root = root ?? Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
        MusicRoot = Path.Combine(Root, "iPod_Control", "Music");
        ITunesRoot = Path.Combine(Root, "iPod_Control", "iTunes");
        Directory.CreateDirectory(Path.Combine(MusicRoot, "F00"));
        Directory.CreateDirectory(Path.Combine(MusicRoot, "F01"));
        Directory.CreateDirectory(ITunesRoot);
    }

    public string Root { get; }
    public string MusicRoot { get; }
    public string ITunesRoot { get; }

    public string MusicFile(string folder, string name) => Path.Combine(MusicRoot, folder, name);

    public void WriteDatabase(ITunesDbBuilder builder) =>
        File.WriteAllBytes(Path.Combine(ITunesRoot, "iTunesDB"), builder.Build());

    public IpodDevice ToDevice(bool hasDatabase = true) => new()
    {
        RootPath = Root,
        VolumeLabel = "IPOD",
        TotalCapacityBytes = 8L * 1024 * 1024 * 1024,
        FreeSpaceBytes = 4L * 1024 * 1024 * 1024,
        DetectionScore = 110,
        HasMusicFolder = true,
        HasITunesDatabase = hasDatabase,
    };

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Volume provider returning a fixed set of volumes.</summary>
public sealed class FakeVolumeProvider : IVolumeInfoProvider
{
    public List<VolumeInfo> Volumes { get; } = new();

    public IReadOnlyList<VolumeInfo> GetMountedVolumes() => Volumes.ToList();
}

/// <summary>
/// Wraps the real filesystem but can simulate the device being unplugged:
/// once <see cref="RemoveDevice"/> is called, everything under the device
/// prefix throws IOException, exactly like a vanished volume.
/// </summary>
public sealed class RemovableFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly string _devicePrefix;
    private volatile bool _removed;
    private long _bytesReadFromDevice;
    private readonly long _removeAfterBytes;

    public RemovableFileSystem(IFileSystem inner, string devicePrefix, long removeAfterBytes = long.MaxValue)
    {
        _inner = inner;
        _devicePrefix = devicePrefix;
        _removeAfterBytes = removeAfterBytes;
    }

    public void RemoveDevice() => _removed = true;

    private bool IsDevicePath(string path) =>
        path.StartsWith(_devicePrefix, StringComparison.OrdinalIgnoreCase);

    private void ThrowIfRemoved(string path)
    {
        if (_removed && IsDevicePath(path))
        {
            throw new IOException("The device is not connected.");
        }
    }

    public bool FileExists(string path)
    {
        if (_removed && IsDevicePath(path))
        {
            return false;
        }

        return _inner.FileExists(path);
    }

    public bool DirectoryExists(string path)
    {
        if (_removed && IsDevicePath(path))
        {
            return false;
        }

        return _inner.DirectoryExists(path);
    }

    public IEnumerable<string> EnumerateFiles(string root, bool recursive)
    {
        ThrowIfRemoved(root);
        return _inner.EnumerateFiles(root, recursive);
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        ThrowIfRemoved(path);
        return _inner.EnumerateDirectories(path);
    }

    public long GetFileSize(string path)
    {
        ThrowIfRemoved(path);
        return _inner.GetFileSize(path);
    }

    public DateTime GetCreationTimeUtc(string path)
    {
        ThrowIfRemoved(path);
        return _inner.GetCreationTimeUtc(path);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        ThrowIfRemoved(path);
        return _inner.GetLastWriteTimeUtc(path);
    }

    public Stream OpenRead(string path)
    {
        ThrowIfRemoved(path);
        var stream = _inner.OpenRead(path);
        return IsDevicePath(path) ? new RemovableStream(stream, this) : stream;
    }

    public void CreateDirectory(string path) => _inner.CreateDirectory(path);

    public Stream CreateWrite(string path) => _inner.CreateWrite(path);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
        _inner.MoveFile(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string path)
    {
        ThrowIfRemoved(path);
        _inner.DeleteFile(path);
    }

    public void SetTimestamps(string path, DateTime createdUtc, DateTime modifiedUtc) =>
        _inner.SetTimestamps(path, createdUtc, modifiedUtc);

    public long GetAvailableFreeSpace(string path) => _inner.GetAvailableFreeSpace(path);

    private void OnDeviceBytesRead(int count)
    {
        if (Interlocked.Add(ref _bytesReadFromDevice, count) >= _removeAfterBytes)
        {
            _removed = true;
        }
    }

    /// <summary>Fails mid-read once the device is "unplugged".</summary>
    private sealed class RemovableStream : Stream
    {
        private readonly Stream _inner;
        private readonly RemovableFileSystem _owner;

        public RemovableStream(Stream inner, RemovableFileSystem owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfRemoved();
            var read = _inner.Read(buffer, offset, count);
            _owner.OnDeviceBytesRead(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfRemoved();
            var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _owner.OnDeviceBytesRead(read);
            ThrowIfRemoved();
            return read;
        }

        private void ThrowIfRemoved()
        {
            if (_owner._removed)
            {
                throw new IOException("The device is not connected.");
            }
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
