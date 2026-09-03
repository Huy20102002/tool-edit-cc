using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoVideoEditor.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;

    public ObservableCollection<JobLogEntry> Logs => _logService.Logs;

    [ObservableProperty]
    private JobLogEntry? _selectedLog;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _logService.ClearLogs();
    }

    [RelayCommand]
    public void CopyAllLogs()
    {
        var sb = new StringBuilder();
        foreach (var l in Logs)
        {
            sb.AppendLine($"[{l.FormattedTimestamp}] [{l.Level}] {l.Message}{(string.IsNullOrEmpty(l.Details) ? "" : $" | {l.Details}")}");
        }

        Clipboard.SetText(sb.ToString());
        MessageBox.Show("Đã sao chép toàn bộ nhật ký vào Clipboard.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void OpenLogsFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AutoVideoEditor", "logs");
        if (Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
    }
}
