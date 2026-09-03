using AutoVideoEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.Services;

public class TempFileManager : ITempFileManager
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TempFileManager>? _logger;

    public TempFileManager(ISettingsService settingsService, ILogger<TempFileManager>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public string CreateJobTempDirectory(Guid jobId)
    {
        var baseTemp = _settingsService.CurrentSettings.TempDirectory;
        if (string.IsNullOrWhiteSpace(baseTemp))
        {
            baseTemp = Path.Combine(Path.GetTempPath(), "AutoVideoEditor");
        }

        var jobDir = Path.Combine(baseTemp, $"Job_{jobId:N}");
        Directory.CreateDirectory(jobDir);
        return jobDir;
    }

    public void CleanupJobTempDirectory(Guid jobId)
    {
        try
        {
            var baseTemp = _settingsService.CurrentSettings.TempDirectory;
            var jobDir = Path.Combine(baseTemp, $"Job_{jobId:N}");
            if (Directory.Exists(jobDir))
            {
                Directory.Delete(jobDir, recursive: true);
                _logger?.LogDebug("Cleaned up temp directory for job {JobId}", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clean up temp directory for job {JobId}", jobId);
        }
    }

    public void CleanupAllOldTempFiles()
    {
        try
        {
            var baseTemp = _settingsService.CurrentSettings.TempDirectory;
            if (Directory.Exists(baseTemp))
            {
                var dirInfo = new DirectoryInfo(baseTemp);
                foreach (var subDir in dirInfo.GetDirectories("Job_*"))
                {
                    try
                    {
                        // Clean temp folders older than 2 hours
                        if (DateTime.Now - subDir.CreationTime > TimeSpan.FromHours(2))
                        {
                            subDir.Delete(recursive: true);
                        }
                    }
                    catch
                    {
                        // Ignore locked folders
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clean up old temp files");
        }
    }
}
