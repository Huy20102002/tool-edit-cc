using System.Windows;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoVideoEditor.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IJobManager _jobManager;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentViewTitle = "Trang chủ";

    [ObservableProperty]
    private string _ffmpegStatusText = "Đang kiểm tra FFmpeg...";

    [ObservableProperty]
    private string _gpuStatusText = "Đang phát hiện GPU...";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _completedJobsCount;

    [ObservableProperty]
    private int _totalJobsCount;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressSummaryText = "Đang xử lý...";

    public HomeViewModel HomeVm { get; }
    public QueueViewModel QueueVm { get; }
    public PreviewViewModel PreviewVm { get; }
    public PresetViewModel PresetVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public LogsViewModel LogsVm { get; }

    public MainViewModel(
        IFFmpegLocator ffmpegLocator,
        IHardwareDetector hardwareDetector,
        IJobManager jobManager,
        ISettingsService settingsService,
        HomeViewModel homeVm,
        QueueViewModel queueVm,
        PreviewViewModel previewVm,
        PresetViewModel presetVm,
        SettingsViewModel settingsVm,
        LogsViewModel logsVm)
    {
        _ffmpegLocator = ffmpegLocator;
        _hardwareDetector = hardwareDetector;
        _jobManager = jobManager;
        _settingsService = settingsService;

        HomeVm = homeVm;
        QueueVm = queueVm;
        PreviewVm = previewVm;
        PresetVm = presetVm;
        SettingsVm = settingsVm;
        LogsVm = logsVm;

        _currentViewModel = HomeVm;

        HomeVm.NavigateToQueueRequested += () => NavigateTo("Queue");

        _jobManager.JobStatusChanged += OnJobStatusChanged;
        _jobManager.JobProgressUpdated += OnJobProgressUpdated;
        _jobManager.QueueFinished += OnQueueFinished;

        _ = InitializeEnvironmentAsync();
    }

    private async Task InitializeEnvironmentAsync()
    {
        await _settingsService.LoadSettingsAsync();

        var isFfmpegOk = _ffmpegLocator.IsFFmpegAvailable();
        var version = _ffmpegLocator.GetFFmpegVersion();
        FfmpegStatusText = isFfmpegOk ? $"FFmpeg: Đã sẵn sàng ({version.Split('\n')[0]})" : "FFmpeg: Chưa tìm thấy! Vui lòng cài đặt hoặc chỉ định đường dẫn";

        var caps = await _hardwareDetector.DetectCapabilitiesAsync();
        GpuStatusText = $"GPU: {caps.GpuName} | Bộ mã hóa: {caps.RecommendedEncoderH264}";
    }

    [RelayCommand]
    public void NavigateTo(string target)
    {
        switch (target)
        {
            case "Home":
                CurrentViewModel = HomeVm;
                CurrentViewTitle = "Trang chủ — Tự động dựng video";
                break;
            case "Queue":
                CurrentViewModel = QueueVm;
                CurrentViewTitle = "Hàng đợi xử lý hàng loạt";
                break;
            case "Preview":
                CurrentViewModel = PreviewVm;
                CurrentViewTitle = "Xem trước & Phân tích khoảng lặng";
                break;
            case "Presets":
                CurrentViewModel = PresetVm;
                CurrentViewTitle = "Quản lý mẫu xuất (Presets)";
                break;
            case "Settings":
                CurrentViewModel = SettingsVm;
                CurrentViewTitle = "Cài đặt hệ thống & Hiệu năng";
                break;
            case "Logs":
                CurrentViewModel = LogsVm;
                CurrentViewTitle = "Nhật ký xử lý & Chi tiết kỹ thuật";
                break;
        }
    }

    private void OnJobStatusChanged(VideoJob job)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateCounts();
        });
    }

    private void OnJobProgressUpdated(VideoJob job, JobProgressReport report)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateCounts();
        });
    }

    private void OnQueueFinished()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            UpdateCounts();
        });
    }

    private void UpdateCounts()
    {
        IsProcessing = _jobManager.IsProcessing;
        TotalJobsCount = _jobManager.Jobs.Count;
        CompletedJobsCount = _jobManager.Jobs.Count(j => j.Status == Core.Enums.JobStatus.Completed);
        OverallProgress = TotalJobsCount > 0 ? (CompletedJobsCount / (double)TotalJobsCount) * 100.0 : 0.0;
        ProgressSummaryText = $"Đã xong {CompletedJobsCount} / {TotalJobsCount} video";
    }
}
