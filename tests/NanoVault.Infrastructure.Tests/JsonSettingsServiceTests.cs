using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Infrastructure.Settings;
using Xunit;

namespace NanoVault.Infrastructure.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private JsonSettingsService Create() => new(
        new PhysicalFileSystem(), NullLogger<JsonSettingsService>.Instance, _workspace.Root);

    [Fact]
    public async Task Round_trips_settings_across_instances()
    {
        var service = Create();
        await service.SaveAsync(new AppSettings
        {
            Template = OrganizationTemplate.FlatAllMusic,
            DuplicateBehavior = DuplicateBehavior.KeepBoth,
            VerifyCopies = false,
            Theme = AppTheme.Dark,
            LastDestination = @"D:\Music Backup",
        });

        var reloaded = Create();
        await reloaded.LoadAsync();

        Assert.Equal(OrganizationTemplate.FlatAllMusic, reloaded.Current.Template);
        Assert.Equal(DuplicateBehavior.KeepBoth, reloaded.Current.DuplicateBehavior);
        Assert.False(reloaded.Current.VerifyCopies);
        Assert.Equal(AppTheme.Dark, reloaded.Current.Theme);
        Assert.Equal(@"D:\Music Backup", reloaded.Current.LastDestination);
    }

    [Fact]
    public async Task Defaults_apply_when_no_settings_exist()
    {
        var service = Create();
        await service.LoadAsync();

        Assert.Equal(OrganizationTemplate.ArtistAlbum, service.Current.Template);
        Assert.Equal(DuplicateBehavior.SkipExactDuplicates, service.Current.DuplicateBehavior);
        Assert.True(service.Current.VerifyCopies);
        Assert.False(service.Current.AnonymousDiagnostics);
    }

    [Fact]
    public async Task Corrupt_settings_file_falls_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_workspace.Root, "settings.json"), "{ not valid json !!");

        var service = Create();
        await service.LoadAsync();

        Assert.True(service.Current.VerifyCopies); // defaults, no crash
    }

    [Fact]
    public async Task Save_raises_the_changed_event()
    {
        var service = Create();
        AppSettings? observed = null;
        service.SettingsChanged += (_, s) => observed = s;

        await service.SaveAsync(new AppSettings { Theme = AppTheme.Light });

        Assert.NotNull(observed);
        Assert.Equal(AppTheme.Light, observed!.Theme);
    }
}
