using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Abstractions;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.TestSupport;
using Xunit;

namespace NanoVault.Ipod.Tests;

public class IpodDiscoveryServiceTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly FakeVolumeProvider _volumes = new();
    private readonly IpodDiscoveryService _discovery;
    private readonly string _plainDrive;

    public IpodDiscoveryServiceTests()
    {
        _discovery = new IpodDiscoveryService(
            _volumes, new PhysicalFileSystem(), NullLogger<IpodDiscoveryService>.Instance);
        _plainDrive = Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_plainDrive);
    }

    public void Dispose()
    {
        _ipod.Dispose();
        try
        {
            Directory.Delete(_plainDrive, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static VolumeInfo Volume(string root, string? label, bool removable = true) => new()
    {
        RootPath = root,
        VolumeLabel = label,
        IsRemovable = removable,
        TotalSizeBytes = 8L << 30,
        FreeSpaceBytes = 2L << 30,
    };

    [Fact]
    public async Task Detects_ipod_by_folder_structure_even_with_renamed_label()
    {
        _ipod.WriteDatabase(new ITunesDbBuilder());
        _volumes.Volumes.Add(Volume(_ipod.Root, "DANNY'S MUSIC"));
        _volumes.Volumes.Add(Volume(_plainDrive, "USB STICK"));

        var found = await _discovery.FindIpodsAsync();

        var device = Assert.Single(found);
        Assert.Equal(_ipod.Root, device.RootPath);
        Assert.True(device.HasMusicFolder);
        Assert.True(device.HasITunesDatabase);
        Assert.Equal("DANNY'S MUSIC", device.VolumeLabel);
    }

    [Fact]
    public async Task Plain_usb_stick_is_never_reported()
    {
        _volumes.Volumes.Add(Volume(_plainDrive, "USB STICK"));
        var found = await _discovery.FindIpodsAsync();
        Assert.Empty(found);
    }

    [Fact]
    public async Task Label_only_device_reports_storage_unavailable_candidate()
    {
        _volumes.Volumes.Add(Volume(_plainDrive, "IPOD"));
        var found = await _discovery.FindIpodsAsync();

        var device = Assert.Single(found);
        Assert.False(device.HasMusicFolder);
    }

    [Fact]
    public async Task Multiple_ipods_are_all_returned_best_first()
    {
        using var second = new SyntheticIpod();
        _ipod.WriteDatabase(new ITunesDbBuilder());

        _volumes.Volumes.Add(Volume(second.Root, null));
        _volumes.Volumes.Add(Volume(_ipod.Root, "IPOD"));

        var found = await _discovery.FindIpodsAsync();

        Assert.Equal(2, found.Count);
        Assert.Equal(_ipod.Root, found[0].RootPath); // has DB + label → higher score
        Assert.True(found[0].DetectionScore >= found[1].DetectionScore);
    }

    [Fact]
    public async Task Case_insensitive_ipod_control_lookup()
    {
        var root = Path.Combine(Path.GetTempPath(), "nanovault-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "IPOD_CONTROL", "MUSIC", "F00"));
        try
        {
            _volumes.Volumes.Add(Volume(root, null));
            var found = await _discovery.FindIpodsAsync();

            var device = Assert.Single(found);
            Assert.True(device.HasMusicFolder);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Unready_volume_is_skipped()
    {
        _volumes.Volumes.Add(Volume(_ipod.Root, "IPOD") with { IsReady = false });
        var found = await _discovery.FindIpodsAsync();
        Assert.Empty(found);
    }
}
