using System.Text.Json;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService>? _logger;
    private AppSettings _currentSettings;

    public AppSettings CurrentSettings => _currentSettings;

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AutoVideoEditor");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        _currentSettings = new AppSettings();
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                    EnsureDirectories(_currentSettings);
                    return _currentSettings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load settings file. Using default settings.");
        }

        _currentSettings = new AppSettings();
        EnsureDirectories(_currentSettings);
        await SaveSettingsAsync(_currentSettings).ConfigureAwait(false);
        return _currentSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        _currentSettings = settings ?? new AppSettings();
        EnsureDirectories(_currentSettings);

        try
        {
            var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
        }
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
                Directory.CreateDirectory(settings.OutputDirectory);

            if (!string.IsNullOrWhiteSpace(settings.TempDirectory))
                Directory.CreateDirectory(settings.TempDirectory);
        }
        catch
        {
            // Ignore directory creation issues if offline/read-only
        }
    }
}
