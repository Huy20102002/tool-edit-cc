using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class TimelineBuilder : ITimelineBuilder
{
    private readonly ITransitionPlanner _transitionPlanner;
    private readonly ILogger<TimelineBuilder>? _logger;

    public TimelineBuilder(
        ITransitionPlanner? transitionPlanner = null,
        ILogger<TimelineBuilder>? logger = null)
    {
        _transitionPlanner = transitionPlanner ?? new TransitionPlanner();
        _logger = logger;
    }

    public TimelinePlan BuildTimeline(
        IReadOnlyList<MediaFileInfo> videoFiles,
        AudioAnalysisResult voiceAnalysis,
        ExportPreset preset,
        double? customTrimStart = null,
        double? customTrimEnd = null,
        double? customExtraEnd = null,
        int? customTransitionCount = null,
        TransitionType? customTransitionType = null,
        IReadOnlyList<SceneSegment>? detectedScenes = null)
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

        // 2. Build Scene Segments for Transition Planning
        var sceneList = new List<SceneSegment>();
        if (detectedScenes != null && detectedScenes.Count > 1)
        {
            // Use detected scenes mapped to timeline duration
            double sPos = 0.0;
            int sIdx = 1;
            foreach (var ds in detectedScenes)
            {
                if (sPos >= targetDuration) break;
                var sDur = Math.Min(ds.DurationSeconds, targetDuration - sPos);
                if (sDur > 0.1)
                {
                    sceneList.Add(new SceneSegment(sIdx++, sPos, sPos + sDur));
                    sPos += sDur;
                }
            }
            if (sPos < targetDuration && sceneList.Count > 0)
            {
                // Extend last scene or add remainder
                var last = sceneList.Last();
                sceneList[sceneList.Count - 1] = new SceneSegment(last.Index, last.StartSeconds, targetDuration);
            }
        }
        else if (plan.VideoSlices.Count > 1)
        {
            for (int i = 0; i < plan.VideoSlices.Count; i++)
            {
                var sl = plan.VideoSlices[i];
                sceneList.Add(new SceneSegment(i + 1, sl.TimelineStartSeconds, sl.TimelineStartSeconds + sl.TimelineDurationSeconds));
            }
        }
        else
        {
            // Single continuous video: Subdivide into aesthetic rhythmic sub-scenes (3s-5s each) based on voice pauses
            double idealSceneDur = 4.0;
            int estimatedScenes = Math.Max(2, (int)Math.Round(targetDuration / idealSceneDur));
            double actualSceneDur = targetDuration / estimatedScenes;

            double sPos = 0.0;
            for (int i = 0; i < estimatedScenes; i++)
            {
                double sEnd = (i == estimatedScenes - 1) ? targetDuration : sPos + actualSceneDur;
                sceneList.Add(new SceneSegment(i + 1, sPos, sEnd));
                sPos = sEnd;
            }
        }

        plan.Scenes = sceneList;

        // 3. Plan Transitions
        var reqCount = customTransitionCount ?? preset.TransitionCount;
        var reqType = customTransitionType ?? preset.TransitionType;
        var reqDur = preset.TransitionDurationSeconds > 0 ? preset.TransitionDurationSeconds : 0.20;
        var minSpacing = preset.MinTransitionSpacingSeconds > 0 ? preset.MinTransitionSpacingSeconds : 2.0;

        if (!preset.EnableTransitions || reqCount <= 0 || reqType == TransitionType.None)
        {
            reqCount = 0;
            reqType = TransitionType.None;
        }

        plan.Transitions = _transitionPlanner.PlanTransitions(
            plan.Scenes,
            reqCount,
            reqType,
            reqDur,
            minSpacing,
            plan.AudioSpeechSegments
        );

        plan.SummaryText = $"Master Duration: {targetDuration:F2}s (+{extraEnd:F1}s outro) | Slices: {plan.VideoSlices.Count} | Scenes: {plan.Scenes.Count} | Transitions: {plan.ActiveTransitionsCount}/{reqCount} ({reqType}) | Loops: {loopCounter}";
        _logger?.LogInformation("Built timeline plan: {Summary}", plan.SummaryText);

        return plan;
    }
}
