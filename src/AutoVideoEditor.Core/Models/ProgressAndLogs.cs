using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class JobProgressReport
{
    public Guid JobId { get; set; }
    public JobStatus Status { get; set; }
    public string StepDescription { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; } // 0 - 100
    public long CurrentFrame { get; set; }
    public long TotalFrames { get; set; }
    public double CurrentFps { get; set; }
    public double Speed { get; set; }
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class JobLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO"; // INFO, SUCCESS, WARN, ERROR
    public string Message { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public string? JobTag { get; set; } // e.g. "[JOB 001]"
    public string? Details { get; set; }

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
    public string DisplayText => string.IsNullOrEmpty(JobTag) 
        ? $"[{FormattedTimestamp}] [{Level}] {Message}"
        : $"[{FormattedTimestamp}] [{Level}] {JobTag} {Message}";
}
