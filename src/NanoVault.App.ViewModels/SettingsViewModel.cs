using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.App.ViewModels;

/// <summary>Simple settings page bound directly to <see cref="AppSettings"/>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private OrganizationTemplate _template;

    [ObservableProperty]
    private DuplicateBehavior _duplicateBehavior;

    [ObservableProperty]
    private bool _verifyCopies;

    [ObservableProperty]
    private bool _preserveTimestamps;

    [ObservableProperty]
    private bool _includeProtectedFiles;

    [ObservableProperty]
    private bool _createMasterPlaylist;

    [ObservableProperty]
    private bool _createIpodPlaylists;

    [ObservableProperty]
    private bool _rememberLastDestination;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private bool _anonymousDiagnostics;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFrom(settingsService.Current);
    }

    /// <summary>Raised after settings are saved, so plans can be refreshed.</summary>
    public event EventHandler<AppSettings>? Saved;

    public void LoadFrom(AppSettings settings)
    {
        Template = settings.Template;
        DuplicateBehavior = settings.DuplicateBehavior;
        VerifyCopies = settings.VerifyCopies;
        PreserveTimestamps = settings.PreserveTimestamps;
        IncludeProtectedFiles = settings.IncludeProtectedFiles;
        CreateMasterPlaylist = settings.CreateMasterPlaylist;
        CreateIpodPlaylists = settings.CreateIpodPlaylists;
        RememberLastDestination = settings.RememberLastDestination;
        Theme = settings.Theme;
        AnonymousDiagnostics = settings.AnonymousDiagnostics;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var updated = _settingsService.Current with
        {
            Template = Template,
            DuplicateBehavior = DuplicateBehavior,
            VerifyCopies = VerifyCopies,
            PreserveTimestamps = PreserveTimestamps,
            IncludeProtectedFiles = IncludeProtectedFiles,
            CreateMasterPlaylist = CreateMasterPlaylist,
            CreateIpodPlaylists = CreateIpodPlaylists,
            RememberLastDestination = RememberLastDestination,
            Theme = Theme,
            AnonymousDiagnostics = AnonymousDiagnostics,
        };

        await _settingsService.SaveAsync(updated).ConfigureAwait(true);
        Saved?.Invoke(this, updated);
    }
}
