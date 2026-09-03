using System.Collections.ObjectModel;
using System.Text;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.Infrastructure.Services;

public class LogService : ILogService
{
    private readonly object _lock = new();
    private readonly string _logFilePath;
    private readonly ObservableCollection<JobLogEntry> _logs = new();

    public ObservableCollection<JobLogEntry> Logs => _logs;
    public object SyncRoot => _lock;
    public event Action<JobLogEntry>? LogAdded;

    public LogService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appData, "AutoVideoEditor", "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public void LogInfo(string message, string? jobId = null, string? jobTag = null, string? details = null)
    {
        AddEntry("INFO", message, jobId, jobTag, details);
    }

    public void LogSuccess(string message, string? jobId = null, string? jobTag = null, string? details = null)
    {
        AddEntry("SUCCESS", message, jobId, jobTag, details);
    }

    public void LogWarning(string message, string? jobId = null, string? jobTag = null, string? details = null)
    {
        AddEntry("WARN", message, jobId, jobTag, details);
    }

    public void LogError(string message, Exception? ex = null, string? jobId = null, string? jobTag = null, string? details = null)
    {
        var fullDetails = details ?? "";
        if (ex != null)
        {
            fullDetails = string.IsNullOrEmpty(fullDetails) 
                ? ex.ToString() 
                : $"{fullDetails}\nException: {ex}";
        }
        AddEntry("ERROR", message, jobId, jobTag, fullDetails);
    }

    public void ClearLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }

    public async Task SaveLogsToFileAsync(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"================================================================================");
        sb.AppendLine($" AUTO VIDEO EDITOR — NHẬT KÝ XỬ LÝ (PROCESSING LOG)");
        sb.AppendLine($" Thời gian xuất log: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"================================================================================");
        sb.AppendLine();

        lock (_lock)
        {
            foreach (var l in _logs)
            {
                sb.AppendLine($"[{l.FormattedTimestamp}] [{l.Level,-7}] {(string.IsNullOrEmpty(l.JobTag) ? "" : $"{l.JobTag} ")}{l.Message}{(string.IsNullOrEmpty(l.Details) ? "" : $"\n  -> Chi tiết: {l.Details}")}");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
    }

    private void AddEntry(string level, string message, string? jobId, string? jobTag, string? details)
    {
        var entry = new JobLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            JobId = jobId,
            JobTag = jobTag,
            Details = details
        };

        lock (_lock)
        {
            if (_logs.Count > 2000)
            {
                _logs.RemoveAt(0);
            }
            _logs.Add(entry);
        }

        try
        {
            var line = $"[{entry.FormattedTimestamp}] [{entry.Level,-7}] {(string.IsNullOrEmpty(entry.JobTag) ? "" : $"{entry.JobTag} ")}{entry.Message}{(string.IsNullOrEmpty(entry.Details) ? "" : $" | {entry.Details}")}{Environment.NewLine}";
            File.AppendAllText(_logFilePath, line);
        }
        catch
        {
            // Ignore file logging errors
        }

        LogAdded?.Invoke(entry);
    }
}
