namespace AutoVideoEditor.Core.Interfaces;

public class CapCutExportItem
{
    public int OrderIndex { get; set; }
    public string VideoPath { get; set; } = string.Empty;
    public string VoicePath { get; set; } = string.Empty;
    public double VideoDurationSeconds { get; set; }
    public double VoiceDurationSeconds { get; set; }
    public double VideoTrimStartSeconds { get; set; }
    public double VideoTrimEndSeconds { get; set; }
    public double VoiceTrimStartSeconds { get; set; }
    public double VoiceTrimEndSeconds { get; set; }
    public double ExtraEndPaddingSeconds { get; set; }
}

public class CapCutExportResult
{
    public bool Success { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public int TimelinesCount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public interface ICapCutDraftService
{
    string? DetectCapCutDraftsRootDirectory();
    Task<CapCutExportResult> ExportMultiTimelineProjectAsync(
        string projectName,
        IReadOnlyList<CapCutExportItem> items,
        string? targetDraftsRootDir = null,
        CancellationToken cancellationToken = default);
}
