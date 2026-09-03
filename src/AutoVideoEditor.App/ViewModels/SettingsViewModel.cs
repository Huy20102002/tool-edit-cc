using System.IO;
using System.Windows;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AutoVideoEditor.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IFFmpegLocator _ffmpegLocator;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _tempDirectory = string.Empty;

    [ObservableProperty]
    private int _maxParallelJobs = 2;

    [ObservableProperty]
    private HardwareEncoderType _hardwareEncoder = HardwareEncoderType.Auto;

    [ObservableProperty]
    private OverwritePolicy _overwritePolicy = OverwritePolicy.AutoRename;

    [ObservableProperty]
    private string _outputNamingPattern = "{original_name}_edited";

    [ObservableProperty]
    private double _defaultSilenceThresholdDb = -35.0;

    [ObservableProperty]
    private int _defaultMinSilenceMs = 400;

    [ObservableProperty]
    private int _defaultPaddingBeforeMs = 80;

    [ObservableProperty]
    private int _defaultPaddingAfterMs = 80;

    [ObservableProperty]
    private string _hardwareInfo = "Chưa kiểm tra";

    public SettingsViewModel(
        ISettingsService settingsService,
        IHardwareDetector hardwareDetector,
        IFFmpegLocator ffmpegLocator)
    {
        _settingsService = settingsService;
        _hardwareDetector = hardwareDetector;
        _ffmpegLocator = ffmpegLocator;

        _ = LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        var s = await _settingsService.LoadSettingsAsync();
        OutputDirectory = s.OutputDirectory;
        TempDirectory = s.TempDirectory;
        MaxParallelJobs = s.MaxParallelJobs;
        HardwareEncoder = s.HardwareEncoderPreference;
        OverwritePolicy = s.OverwritePolicy;
        OutputNamingPattern = s.OutputNamingPattern;
        DefaultSilenceThresholdDb = s.DefaultSilenceThresholdDb;
        DefaultMinSilenceMs = s.DefaultMinSilenceMs;
        DefaultPaddingBeforeMs = s.DefaultPaddingBeforeMs;
        DefaultPaddingAfterMs = s.DefaultPaddingAfterMs;
    }

    [RelayCommand]
    public void ChooseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục xuất video mặc định" };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            OutputDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    public void ChooseTempFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục lưu file tạm" };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            TempDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    public async Task CheckHardwareAccelerationAsync()
    {
        var caps = await _hardwareDetector.DetectCapabilitiesAsync();
        HardwareInfo = $"GPU: {caps.GpuName}\n" +
                       $"• NVIDIA NVENC (H.264 / HEVC): {(caps.SupportsNvencH264 ? "Hỗ trợ ✓" : "Không")}\n" +
                       $"• Intel QuickSync QSV: {(caps.SupportsQsvH264 ? "Hỗ trợ ✓" : "Không")}\n" +
                       $"• AMD AMF: {(caps.SupportsAmfH264 ? "Hỗ trợ ✓" : "Không")}\n" +
                       $"• CPU Cores: {caps.LogicalCores}\n" +
                       $"• Bộ mã hóa khuyên dùng: {caps.RecommendedEncoderH264}";

        MessageBox.Show(HardwareInfo, "Kết quả kiểm tra phần cứng", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var s = _settingsService.CurrentSettings;
        s.OutputDirectory = OutputDirectory;
        s.TempDirectory = TempDirectory;
        s.MaxParallelJobs = MaxParallelJobs;
        s.HardwareEncoderPreference = HardwareEncoder;
        s.OverwritePolicy = OverwritePolicy;
        s.OutputNamingPattern = OutputNamingPattern;
        s.DefaultSilenceThresholdDb = DefaultSilenceThresholdDb;
        s.DefaultMinSilenceMs = DefaultMinSilenceMs;
        s.DefaultPaddingBeforeMs = DefaultPaddingBeforeMs;
        s.DefaultPaddingAfterMs = DefaultPaddingAfterMs;

        await _settingsService.SaveSettingsAsync(s);
        MessageBox.Show("Đã lưu cài đặt thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
