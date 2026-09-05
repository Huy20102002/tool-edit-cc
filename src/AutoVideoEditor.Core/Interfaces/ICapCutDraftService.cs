using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Interfaces;

public class CapCutProjectTemplateInfo
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int TracksCount { get; set; }
    public int TextsCount { get; set; }
    public int StickersCount { get; set; }
    public int AudiosCount { get; set; }
    public DateTime LastModified { get; set; }

    public string DisplayName => TextsCount > 0 || StickersCount > 0
        ? $"{Name} ({TextsCount} chữ, {StickersCount} sticker)"
        : Name;
}

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
    public bool MuteOriginalAudio { get; set; } = true;
    public int TransitionCount { get; set; } = 2;
    public TransitionType TransitionType { get; set; } = TransitionType.Smart;
}

public class CapCutExportResult
{
    public bool Success { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public int TimelinesCount { get; set; }
    public string TemplateUsed { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public interface ICapCutDraftService
{
    string? DetectCapCutDraftsRootDirectory();
    List<CapCutProjectTemplateInfo> GetAvailableTemplates(string? customDraftsRootDir = null);
    Task<CapCutExportResult> ExportMultiTimelineProjectAsync(
        string projectName,
        IReadOnlyList<CapCutExportItem> items,
        string? targetDraftsRootDir = null,
        string? templateFolderPath = null,
        CancellationToken cancellationToken = default);
}
