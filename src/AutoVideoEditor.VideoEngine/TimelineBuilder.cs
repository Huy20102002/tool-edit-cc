using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class TimelineBuilder : ITimelineBuilder
{
    private readonly ILogger<TimelineBuilder>? _logger;

    public TimelineBuilder(ILogger<TimelineBuilder>? logger = null)
    {
        _logger = logger;
    }

    public TimelinePlan BuildTimeline(
        IReadOnlyList<MediaFileInfo> videoFiles,
        AudioAnalysisResult voiceAnalysis,
        ExportPreset preset,
        double? customTrimStart = null,
        double? customTrimEnd = null,
        double? customExtraEnd = null)
    {
        if (videoFiles == null || videoFiles.Count == 0)
        {
            throw new ArgumentException("At least one video file is required to build a timeline.", nameof(videoFiles));
        }

        var extraEnd = Math.Max(0.0, customExtraEnd ?? preset.ExtraEndPaddingSeconds);
        var targetDuration = voiceAnalysis.ProcessedDurationSeconds + extraEnd;
        if (targetDuration <= 0)
        {
            targetDuration = voiceAnalysis.OriginalDurationSeconds > 0 
                ? voiceAnalysis.OriginalDurationSeconds + extraEnd
                : 10.0; // fallback safety
        }

        var plan = new TimelinePlan
        {
            TargetMasterDurationSeconds = targetDuration,
            AudioSpeechSegments = voiceAnalysis.SpeechSegments.ToList()
        };

        var validVideos = videoFiles.Where(v => v.DurationSeconds > 0).ToList();
        if (validVideos.Count == 0)
        {
            validVideos = videoFiles.ToList();
        }

        var trimStart = Math.Max(0.0, customTrimStart ?? preset.VideoTrimStartSeconds);
        var trimEnd = Math.Max(0.0, customTrimEnd ?? preset.VideoTrimEndSeconds);

        double currentTimelinePos = 0.0;
        int loopCounter = 0;

        // Loop until target master duration is completely covered
        while (currentTimelinePos < targetDuration)
        {
            foreach (var video in validVideos)
            {
                if (currentTimelinePos >= targetDuration)
                    break;

                var remainingDurationNeeded = targetDuration - currentTimelinePos;
                var rawSourceDuration = video.DurationSeconds > 0 ? video.DurationSeconds : remainingDurationNeeded;
                var effectiveSourceDuration = Math.Max(0.2, rawSourceDuration - trimStart - trimEnd);

                var sliceDuration = Math.Min(effectiveSourceDuration, remainingDurationNeeded);

                var slice = new VideoTimelineSlice
                {
                    VideoFilePath = video.FilePath,
                    SourceStartSeconds = trimStart,
                    SourceDurationSeconds = sliceDuration,
                    TimelineStartSeconds = currentTimelinePos,
                    LoopIndex = loopCounter
                };

                plan.VideoSlices.Add(slice);
                currentTimelinePos += sliceDuration;
            }

            loopCounter++;
        }

        var firstEffective = Math.Max(0.2, (validVideos[0].DurationSeconds > 0 ? validVideos[0].DurationSeconds : targetDuration) - trimStart - trimEnd);
        plan.RequiresVideoLooping = loopCounter > 1 || (validVideos.Count == 1 && firstEffective < targetDuration);
        plan.RequiresVideoTrimming = validVideos.Count == 1 && firstEffective > targetDuration;
        plan.TotalVideoLoops = loopCounter;

        plan.SummaryText = $"Master Duration: {targetDuration:F2}s (+{extraEnd:F1}s outro) | Slices: {plan.VideoSlices.Count} | Loops: {loopCounter} | Trim: -{trimStart:F1}s / -{trimEnd:F1}s | Speech segments: {plan.AudioSpeechSegments.Count}";
        _logger?.LogInformation("Built timeline plan: {Summary}", plan.SummaryText);

        return plan;
    }
}
