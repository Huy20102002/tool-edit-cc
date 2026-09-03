using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.Core.Interfaces;

public interface ISilenceDetector
{
    Task<AudioAnalysisResult> AnalyzeSilenceAsync(
        string audioFilePath,
        double silenceThresholdDb,
        int minSilenceDurationMs,
        int paddingBeforeMs,
        int paddingAfterMs,
        CancellationToken cancellationToken = default);
}

public interface IWaveformGenerator
{
    Task<float[]> GenerateWaveformAsync(
        string audioFilePath,
        int sampleCount = 400,
        CancellationToken cancellationToken = default);
}

public interface IAudioAnalyzer
{
    Task<AudioAnalysisResult> AnalyzeVoiceAsync(
        string audioFilePath,
        double silenceThresholdDb,
        int minSilenceDurationMs,
        int paddingBeforeMs,
        int paddingAfterMs,
        int waveformSamples = 400,
        CancellationToken cancellationToken = default);
}

public interface ISceneDetector
{
    Task<List<SceneSegment>> DetectScenesAsync(
        string videoFilePath,
        double sceneThreshold = 0.3,
        CancellationToken cancellationToken = default);
}

public interface IVideoAnalyzer
{
    Task<VideoAnalysisResult> AnalyzeVideoAsync(
        string videoFilePath,
        bool detectScenes = false,
        CancellationToken cancellationToken = default);
}

public interface ITimelineBuilder
{
    TimelinePlan BuildTimeline(
        IReadOnlyList<MediaFileInfo> videoFiles,
        AudioAnalysisResult voiceAnalysis,
        ExportPreset preset,
        double? customTrimStart = null,
        double? customTrimEnd = null,
        double? customExtraEnd = null);
}

public interface IVideoRenderer
{
    Task RenderAsync(
        VideoJob job,
        TimelinePlan timelinePlan,
        Action<JobProgressReport> onProgress,
        CancellationToken cancellationToken = default);
}
