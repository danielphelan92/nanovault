using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;

namespace NanoVault.Infrastructure.Settings;

/// <summary>
/// Persists preferences as JSON under the user's local application data
/// folder (%LOCALAPPDATA%\NanoVault). Corrupt or missing settings quietly
/// fall back to safe defaults. Saves are atomic (temp file + rename).
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _settingsPath;
    private readonly object _gate = new();

    private AppSettings _current = new();

    public JsonSettingsService(
        IFileSystem fileSystem,
        ILogger<JsonSettingsService> logger,
        string? settingsDirectory = null)
    {
        _fileSystem = fileSystem;
        _logger = logger;

        var directory = settingsDirectory ?? DefaultSettingsDirectory;
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public static string DefaultSettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NanoVault");

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_fileSystem.FileExists(_settingsPath))
            {
                return;
            }

            var stream = _fileSystem.OpenRead(_settingsPath);
            AppSettings? loaded;
            await using (stream.ConfigureAwait(false))
            {
                loaded = await JsonSerializer
                    .DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (loaded is not null)
            {
                lock (_gate)
                {
                    _current = loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Settings could not be loaded; using defaults");
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current = settings;
        }

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            var temp = _settingsPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, SerializerOptions);

            var stream = _fileSystem.CreateWrite(temp);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
            }

            _fileSystem.MoveFile(temp, _settingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings could not be saved");
        }

        SettingsChanged?.Invoke(this, settings);
    }
}
