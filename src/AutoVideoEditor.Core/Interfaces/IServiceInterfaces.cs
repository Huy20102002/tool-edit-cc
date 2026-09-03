using System.Collections.ObjectModel;
using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.Core.Interfaces;

public interface IJobManager
{
    ObservableCollection<VideoJob> Jobs { get; }
    object SyncRoot { get; }
    bool IsProcessing { get; }
    bool IsPaused { get; }

    event Action<VideoJob>? JobStatusChanged;
    event Action<VideoJob, JobProgressReport>? JobProgressUpdated;
    event Action? QueueFinished;

    void AddJob(VideoJob job);
    void AddJobs(IEnumerable<VideoJob> jobs);
    void RemoveJob(Guid jobId);
    void ClearAllJobs();
    void ClearCompletedJobs();

    Task StartQueueAsync(CancellationToken cancellationToken = default);
    Task PauseQueueAsync();
    Task ResumeQueueAsync();
    Task CancelJobAsync(Guid jobId);
    Task CancelAllAsync();
    Task RetryFailedJobsAsync();
}

public interface IPresetService
{
    Task<List<ExportPreset>> GetAllPresetsAsync();
    Task<ExportPreset> GetPresetByIdAsync(Guid id);
    Task SavePresetAsync(ExportPreset preset);
    Task DeletePresetAsync(Guid id);
    ExportPreset GetDefaultPreset();
}

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    AppSettings CurrentSettings { get; }
}

public interface ILogService
{
    ObservableCollection<JobLogEntry> Logs { get; }
    object SyncRoot { get; }
    event Action<JobLogEntry>? LogAdded;

    void LogInfo(string message, string? jobId = null, string? jobTag = null, string? details = null);
    void LogSuccess(string message, string? jobId = null, string? jobTag = null, string? details = null);
    void LogWarning(string message, string? jobId = null, string? jobTag = null, string? details = null);
    void LogError(string message, Exception? ex = null, string? jobId = null, string? jobTag = null, string? details = null);
    void ClearLogs();
    Task SaveLogsToFileAsync(string filePath);
}

public interface ITempFileManager
{
    string CreateJobTempDirectory(Guid jobId);
    void CleanupJobTempDirectory(Guid jobId);
    void CleanupAllOldTempFiles();
}
