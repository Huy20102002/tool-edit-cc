using System.Globalization;
using System.Text.RegularExpressions;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class SceneDetector : ISceneDetector
{
    private readonly IFFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly IFFprobeService _probeService;
    private readonly ILogger<SceneDetector>? _logger;

    private static readonly Regex PtsTimeRegex = new(@"pts_time:\s*([0-9\.]+)", RegexOptions.Compiled);

    public SceneDetector(
        IFFmpegLocator locator,
        IFFmpegProcessRunner runner,
        IFFprobeService probeService,
        ILogger<SceneDetector>? logger = null)
    {
        _locator = locator;
        _runner = runner;
        _probeService = probeService;
        _logger = logger;
    }

    public async Task<List<SceneSegment>> DetectScenesAsync(
        string videoFilePath,
        double sceneThreshold = 0.3,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoFilePath))
        {
            return new List<SceneSegment>();
        }

        var scenes = new List<SceneSegment>();
        try
        {
            var mediaInfo = await _probeService.ProbeFileAsync(videoFilePath, cancellationToken).ConfigureAwait(false);
            var totalDuration = mediaInfo.DurationSeconds;

            if (totalDuration <= 0) return scenes;

            var ffmpegPath = _locator.GetFFmpegPath();
            var threshStr = sceneThreshold.ToString("F2", CultureInfo.InvariantCulture);
            var arguments = $"-an -sn -i \"{videoFilePath}\" -vf \"scale=-2:240,select='gt(scene,{threshStr})',showinfo\" -f null -";

            var cutPoints = new List<double> { 0.0 };

            await _runner.ExecuteAsync(
                ffmpegPath,
                arguments,
                null,
                line =>
                {
                    if (line == null) return;
                    if (line.Contains("showinfo") && line.Contains("pts_time:"))
                    {
                        var match = PtsTimeRegex.Match(line);
                        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pts))
                        {
                            if (pts > cutPoints.Last() + 0.5 && pts < totalDuration - 0.5)
                            {
                                cutPoints.Add(pts);
                            }
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (!cutPoints.Contains(totalDuration))
            {
                cutPoints.Add(totalDuration);
            }

            for (int i = 0; i < cutPoints.Count - 1; i++)
            {
                var start = cutPoints[i];
                var end = cutPoints[i + 1];
                if (end > start)
                {
                    scenes.Add(new SceneSegment(i + 1, start, end));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Scene detection failed or skipped for {Path}", videoFilePath);
        }

        return scenes;
    }
}
