using System.Collections.ObjectModel;
using System.Windows;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoVideoEditor.App.ViewModels;

public partial class PresetViewModel : ObservableObject
{
    private readonly IPresetService _presetService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<ExportPreset> _presets = new();

    [ObservableProperty]
    private ExportPreset? _selectedPreset;

    [ObservableProperty]
    private bool _isEditing;

    public PresetViewModel(IPresetService presetService, ISettingsService settingsService)
    {
        _presetService = presetService;
        _settingsService = settingsService;
        _ = LoadPresetsAsync();
    }

    public async Task LoadPresetsAsync()
    {
        var list = await _presetService.GetAllPresetsAsync();
        Presets = new ObservableCollection<ExportPreset>(list);
        SelectedPreset = Presets.FirstOrDefault();
    }

    [RelayCommand]
    public async Task DuplicatePresetAsync()
    {
        if (SelectedPreset == null) return;

        var cloned = SelectedPreset.Clone($"{SelectedPreset.Name} (Bản sao)");
        await _presetService.SavePresetAsync(cloned);
        await LoadPresetsAsync();
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == cloned.Id);
    }

    [RelayCommand]
    public async Task SaveCurrentPresetAsync()
    {
        if (SelectedPreset == null) return;

        await _presetService.SavePresetAsync(SelectedPreset);
        MessageBox.Show("Đã lưu mẫu xuất thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public async Task DeletePresetAsync()
    {
        if (SelectedPreset == null || SelectedPreset.IsBuiltIn)
        {
            MessageBox.Show("Không thể xóa mẫu xuất mặc định của hệ thống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show($"Xác nhận xóa mẫu xuất '{SelectedPreset.Name}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            await _presetService.DeletePresetAsync(SelectedPreset.Id);
            await LoadPresetsAsync();
        }
    }

    [RelayCommand]
    public async Task SetAsDefaultPresetAsync()
    {
        if (SelectedPreset == null) return;

        _settingsService.CurrentSettings.DefaultPresetId = SelectedPreset.Id;
        await _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings);
        MessageBox.Show($"Đã đặt '{SelectedPreset.Name}' làm mẫu xuất mặc định.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
