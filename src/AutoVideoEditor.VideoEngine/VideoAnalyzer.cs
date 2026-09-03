using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class VideoAnalyzer : IVideoAnalyzer
{
    private readonly IFFprobeService _probeService;
    private readonly ISceneDetector _sceneDetector;
    private readonly ILogger<VideoAnalyzer>? _logger;

    public VideoAnalyzer(
        IFFprobeService probeService,
        ISceneDetector sceneDetector,
        ILogger<VideoAnalyzer>? logger = null)
    {
        _probeService = probeService;
        _sceneDetector = sceneDetector;
        _logger = logger;
    }

    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(
        string videoFilePath,
        bool detectScenes = false,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Analyzing video file: {Path}", videoFilePath);

        var mediaInfo = await _probeService.ProbeFileAsync(videoFilePath, cancellationToken).ConfigureAwait(false);
        var result = new VideoAnalysisResult
        {
            FilePath = videoFilePath,
            DurationSeconds = mediaInfo.DurationSeconds,
            Width = mediaInfo.Width,
            Height = mediaInfo.Height,
            Fps = mediaInfo.Fps,
            VideoCodec = mediaInfo.VideoCodec
        };

        if (detectScenes && mediaInfo.DurationSeconds > 3.0)
        {
            result.SceneSegments = await _sceneDetector.DetectScenesAsync(videoFilePath, 0.3, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
