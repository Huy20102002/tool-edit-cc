using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AutoVideoEditor.App.ViewModels;

public partial class QueueViewModel : ObservableObject
{
    private readonly IJobManager _jobManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    public ObservableCollection<VideoJob> Jobs => _jobManager.Jobs;
    public ObservableCollection<JobLogEntry> Logs => _logService.Logs;

    [ObservableProperty]
    private VideoJob? _selectedJob;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _processingCount;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _batchSummaryText = "Sẵn sàng";

    public event Action? RequestScrollToLatest;

    public QueueViewModel(
        IJobManager jobManager,
        ISettingsService settingsService,
        ILogService logService)
    {
        _jobManager = jobManager;
        _settingsService = settingsService;
        _logService = logService;

        _jobManager.JobStatusChanged += _ => UpdateStats();
        _jobManager.JobProgressUpdated += (_, _) => UpdateStats();
        _jobManager.QueueFinished += UpdateStats;
    }

    private void UpdateStats()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsProcessing = _jobManager.IsProcessing;
            IsPaused = _jobManager.IsPaused;
            TotalCount = _jobManager.Jobs.Count;
            CompletedCount = _jobManager.Jobs.Count(j => j.Status == Core.Enums.JobStatus.Completed);
            FailedCount = _jobManager.Jobs.Count(j => j.Status == Core.Enums.JobStatus.Failed);
            ProcessingCount = _jobManager.Jobs.Count(j => j.Status == Core.Enums.JobStatus.AnalyzingVoice 
                                                       || j.Status == Core.Enums.JobStatus.DetectingSilence 
                                                       || j.Status == Core.Enums.JobStatus.AnalyzingVideo 
                                                       || j.Status == Core.Enums.JobStatus.BuildingTimeline 
                                                       || j.Status == Core.Enums.JobStatus.Rendering);
            PendingCount = _jobManager.Jobs.Count(j => j.Status == Core.Enums.JobStatus.Pending);
            OverallProgress = TotalCount > 0 ? (CompletedCount / (double)TotalCount) * 100.0 : 0.0;

            if (IsProcessing)
            {
                BatchSummaryText = $"ĐANG XỬ LÝ: {CompletedCount + ProcessingCount} / {TotalCount}";
            }
            else if (CompletedCount > 0 && CompletedCount == TotalCount)
            {
                BatchSummaryText = $"HOÀN TẤT: {CompletedCount} / {TotalCount} VIDEO";
            }
            else
            {
                BatchSummaryText = TotalCount > 0 ? $"HÀNG ĐỢI: {TotalCount} VIDEO" : "CHƯA CÓ VIDEO TRONG HÀNG ĐỢI";
            }
        });
    }

    [RelayCommand]
    public async Task StartQueueAsync()
    {
        if (_jobManager.IsPaused)
        {
            await _jobManager.ResumeQueueAsync();
        }
        else
        {
            _ = Task.Run(() => _jobManager.StartQueueAsync());
        }
        UpdateStats();
    }

    [RelayCommand]
    public async Task PauseQueueAsync()
    {
        await _jobManager.PauseQueueAsync();
        UpdateStats();
    }

    [RelayCommand]
    public async Task CancelAllAsync()
    {
        var result = MessageBox.Show("Bạn có chắc chắn muốn hủy tất cả các công việc trong hàng đợi?", "Xác nhận hủy", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            await _jobManager.CancelAllAsync();
            UpdateStats();
        }
    }

    [RelayCommand]
    public async Task RetryFailedAsync()
    {
        await _jobManager.RetryFailedJobsAsync();
        UpdateStats();
    }

    [RelayCommand]
    public void ClearCompleted()
    {
        _jobManager.ClearCompletedJobs();
        UpdateStats();
    }

    [RelayCommand]
    public void ClearAll()
    {
        if (Jobs.Count == 0) return;

        var result = MessageBox.Show("Xóa toàn bộ danh sách công việc?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _jobManager.ClearAllJobs();
            UpdateStats();
        }
    }

    [RelayCommand]
    public void OpenOutputFolder()
    {
        var dir = _settingsService.CurrentSettings.OutputDirectory;
        if (Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    public void OpenJobOutputFile(VideoJob? job)
    {
        var target = job ?? SelectedJob;
        if (target != null && !string.IsNullOrEmpty(target.OutputPath) && File.Exists(target.OutputPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target.OutputPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    public async Task CancelSingleJobAsync(VideoJob? job)
    {
        if (job != null)
        {
            await _jobManager.CancelJobAsync(job.Id);
            UpdateStats();
        }
    }

    [RelayCommand]
    public void RemoveSingleJob(VideoJob? job)
    {
        if (job != null)
        {
            _jobManager.RemoveJob(job.Id);
            UpdateStats();
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _logService.ClearLogs();
    }

    [RelayCommand]
    public async Task SaveLogsToFileAsync()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Lưu nhật ký xử lý",
            Filter = "Log File (*.log)|*.log|Text File (*.txt)|*.txt",
            FileName = $"AutoVideoEditor_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (sfd.ShowDialog() == true)
        {
            await _logService.SaveLogsToFileAsync(sfd.FileName);
            MessageBox.Show($"Đã lưu nhật ký thành công tại:\n{sfd.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void CopyLogsToClipboard()
    {
        var allLogs = string.Join(Environment.NewLine, Logs.Select(l => l.DisplayText));
        if (!string.IsNullOrEmpty(allLogs))
        {
            Clipboard.SetText(allLogs);
            MessageBox.Show("Đã sao chép nhật ký vào Clipboard!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void ScrollToLatest()
    {
        RequestScrollToLatest?.Invoke();
    }
}
