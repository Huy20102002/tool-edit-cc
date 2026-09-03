using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class VideoJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OrderIndex { get; set; }
    public List<string> VideoPaths { get; set; } = new();
    public string VoicePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public ExportPreset Preset { get; set; } = new();

    // Per-job custom trim & outro padding overrides
    public double VideoTrimStartSeconds { get; set; }
    public double VideoTrimEndSeconds { get; set; }
    public double VoiceTrimStartSeconds { get; set; }
    public double VoiceTrimEndSeconds { get; set; }
    public double ExtraEndPaddingSeconds { get; set; } // Dư cuối video (giây)

    // Primary Display Names
    public string VideoFileName => VideoPaths.Count == 1 
        ? Path.GetFileName(VideoPaths[0]) 
        : VideoPaths.Count > 1 
            ? $"{Path.GetFileName(VideoPaths[0])} (+{VideoPaths.Count - 1} video khác)" 
            : "(Chưa chọn video)";

    public string VoiceFileName => !string.IsNullOrEmpty(VoicePath) 
        ? Path.GetFileName(VoicePath) 
        : "(Chưa chọn giọng đọc)";

    public string OutputFileName => !string.IsNullOrEmpty(OutputPath) 
        ? Path.GetFileName(OutputPath) 
        : "(Chưa tạo)";

    // Execution state
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public double ProgressPercentage { get; set; }
    public string CurrentStepDescription { get; set; } = "Đang chờ";
    public double CurrentFps { get; set; }
    public double Speed { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string? ErrorMessage { get; set; }
    public string? EncoderUsed { get; set; }

    // Time Tracking
    public DateTime? CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan? ElapsedTime => StartedAt.HasValue 
        ? ((CompletedAt ?? DateTime.Now) - StartedAt.Value) 
        : null;

    // Analysis Results cache
    public AudioAnalysisResult? VoiceAnalysis { get; set; }
    public List<MediaFileInfo> VideoMetadatas { get; set; } = new();
    public TimelinePlan? TimelinePlan { get; set; }

    public List<string> InternalLogs { get; set; } = new();

    public void AddLog(string message)
    {
        InternalLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
