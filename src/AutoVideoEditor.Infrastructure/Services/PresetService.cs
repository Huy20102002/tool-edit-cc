using System.Text.Json;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.Services;

public class PresetService : IPresetService
{
    private readonly string _presetsFilePath;
    private readonly ILogger<PresetService>? _logger;
    private List<ExportPreset> _cachedPresets = new();

    public PresetService(ILogger<PresetService>? logger = null)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AutoVideoEditor");
        Directory.CreateDirectory(dir);
        _presetsFilePath = Path.Combine(dir, "presets.json");
    }

    public async Task<List<ExportPreset>> GetAllPresetsAsync()
    {
        if (_cachedPresets.Count > 0)
            return _cachedPresets.ToList();

        var defaults = ExportPreset.GetDefaultPresets();
        var userPresets = new List<ExportPreset>();

        try
        {
            if (File.Exists(_presetsFilePath))
            {
                var json = await File.ReadAllTextAsync(_presetsFilePath).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<List<ExportPreset>>(json);
                if (loaded != null)
                {
                    userPresets = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load custom presets");
        }

        // Merge defaults with custom presets without duplicates
        var dict = new Dictionary<Guid, ExportPreset>();
        foreach (var def in defaults)
        {
            dict[def.Id] = def;
        }
        foreach (var up in userPresets)
        {
            dict[up.Id] = up;
        }

        _cachedPresets = dict.Values.OrderByDescending(p => p.IsBuiltIn).ThenBy(p => p.Name).ToList();
        return _cachedPresets.ToList();
    }

    public async Task<ExportPreset> GetPresetByIdAsync(Guid id)
    {
        var all = await GetAllPresetsAsync().ConfigureAwait(false);
        var found = all.FirstOrDefault(p => p.Id == id);
        return found ?? GetDefaultPreset();
    }

    public async Task SavePresetAsync(ExportPreset preset)
    {
        var all = await GetAllPresetsAsync().ConfigureAwait(false);
        var existingIdx = all.FindIndex(p => p.Id == preset.Id);
        if (existingIdx >= 0)
        {
            all[existingIdx] = preset;
        }
        else
        {
            all.Add(preset);
        }

        _cachedPresets = all;

        // Save only non-built-in or overridden presets
        var customPresets = all.Where(p => !p.IsBuiltIn).ToList();
        try
        {
            var json = JsonSerializer.Serialize(customPresets, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_presetsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save custom presets");
        }
    }

    public async Task DeletePresetAsync(Guid id)
    {
        var all = await GetAllPresetsAsync().ConfigureAwait(false);
        var target = all.FirstOrDefault(p => p.Id == id);
        if (target != null && !target.IsBuiltIn)
        {
            all.Remove(target);
            _cachedPresets = all;

            var customPresets = all.Where(p => !p.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(customPresets, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_presetsFilePath, json).ConfigureAwait(false);
        }
    }

    public ExportPreset GetDefaultPreset()
    {
        return ExportPreset.GetDefaultPresets().First();
    }
}
