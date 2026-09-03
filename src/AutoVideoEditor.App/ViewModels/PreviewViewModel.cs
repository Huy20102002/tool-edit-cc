using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AutoVideoEditor.App.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    private readonly IAudioAnalyzer _audioAnalyzer;
    private readonly ISettingsService _settingsService;
    private readonly IPresetService _presetService;

    [ObservableProperty]
    private string _currentVoiceFilePath = string.Empty;

    [ObservableProperty]
    private string _currentVoiceFileName = "Chưa chọn file giọng đọc";

    [ObservableProperty]
    private AudioAnalysisResult? _analysisResult;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private double _silenceThresholdDb = -35.0;

    [ObservableProperty]
    private int _minSilenceMs = 400;

    [ObservableProperty]
    private int _paddingBeforeMs = 80;

    [ObservableProperty]
    private int _paddingAfterMs = 80;

    public ObservableCollection<SpeechSegment> SpeechSegments { get; } = new();
    public ObservableCollection<SilenceSegment> SilenceSegments { get; } = new();

    public PreviewViewModel(
        IAudioAnalyzer audioAnalyzer,
        ISettingsService settingsService,
        IPresetService presetService)
    {
        _audioAnalyzer = audioAnalyzer;
        _settingsService = settingsService;
        _presetService = presetService;

        var defaults = _settingsService.CurrentSettings;
        if (defaults != null)
        {
            SilenceThresholdDb = defaults.DefaultSilenceThresholdDb;
            MinSilenceMs = defaults.DefaultMinSilenceMs;
            PaddingBeforeMs = defaults.DefaultPaddingBeforeMs;
            PaddingAfterMs = defaults.DefaultPaddingAfterMs;
        }
    }

    [RelayCommand]
    public void SelectVoiceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file giọng đọc để phân tích waveform",
            Filter = "Audio Files (*.mp3;*.wav;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.m4a;*.aac;*.flac|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            CurrentVoiceFilePath = dialog.FileName;
            CurrentVoiceFileName = Path.GetFileName(dialog.FileName);
            _ = AnalyzeCurrentVoiceAsync();
        }
    }

    [RelayCommand]
    public async Task AnalyzeCurrentVoiceAsync()
    {
        if (string.IsNullOrEmpty(CurrentVoiceFilePath) || !File.Exists(CurrentVoiceFilePath))
        {
            return;
        }

        IsAnalyzing = true;
        try
        {
            var result = await _audioAnalyzer.AnalyzeVoiceAsync(
                CurrentVoiceFilePath,
                SilenceThresholdDb,
                MinSilenceMs,
                PaddingBeforeMs,
                PaddingAfterMs,
                500);

            AnalysisResult = result;

            SpeechSegments.Clear();
            foreach (var seg in result.SpeechSegments)
            {
                SpeechSegments.Add(seg);
            }

            SilenceSegments.Clear();
            foreach (var sil in result.SilenceSegments)
            {
                SilenceSegments.Add(sil);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi phân tích âm thanh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    public void LoadVoiceFromJob(string voicePath)
    {
        if (File.Exists(voicePath))
        {
            CurrentVoiceFilePath = voicePath;
            CurrentVoiceFileName = Path.GetFileName(voicePath);
            _ = AnalyzeCurrentVoiceAsync();
        }
    }
}
