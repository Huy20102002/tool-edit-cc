namespace AutoVideoEditor.Core.Enums;

public enum JobStatus
{
    Pending,
    AnalyzingVoice,
    DetectingSilence,
    AnalyzingVideo,
    BuildingTimeline,
    Rendering,
    Completed,
    Failed,
    Canceled,
    Paused
}

public enum AspectRatioMode
{
    Ratio9x16,  // 1080x1920 (TikTok, Reels, Shorts)
    Ratio16x9,  // 1920x1080 (YouTube)
    Ratio1x1,   // 1080x1080 (Square)
    Ratio4x5,   // 1080x1350 (Instagram/FB feed)
    Custom
}

public enum CropMode
{
    FitWithBlur,    // Blurred video background + centered original aspect video (CapCut style)
    CenterCrop,     // Scale to cover and crop center
    FitBlackBars,   // Scale to fit inside frame with black bars
    Stretch         // Direct stretch to target dimensions
}

public enum VideoCodecType
{
    H264,
    H265
}

public enum AudioCodecType
{
    AAC,
    MP3
}

public enum HardwareEncoderType
{
    Auto,
    CPU,
    NvidiaNvenc,
    AmdAmf,
    IntelQsv
}

public enum OverwritePolicy
{
    AutoRename,
    Overwrite,
    Skip
}

public enum FileMappingMode
{
    ByName,
    ByOrder,
    Manual
}

public enum VideoBitrateMode
{
    Auto,
    Low,
    Medium,
    High,
    Custom
}
