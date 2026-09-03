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

        var oneShotClips = new List<OneShotClipSegment>();
        var sceneList = new List<SceneSegment>();

        if (validVideos.Count == 1)
        {
            var video = validVideos[0];
            var rawVideoDuration = video.DurationSeconds > 0 ? video.DurationSeconds : targetDuration;
            var effectiveSourceDuration = Math.Max(1.0, rawVideoDuration - trimStart - trimEnd);

            // 1. OneShot Smart Cut Algorithm (Tạo nhịp Jump Cut từ 1 Video duy nhất)
            if (preset.EnableSmartCut && effectiveSourceDuration > (targetDuration + 1.5))
            {
                // Video is longer than audio: Extract K rhythmic jump-cut segments across the entire video
                int k = Math.Clamp((int)Math.Round(targetDuration / 3.8), 3, 14);
                double clipDur = targetDuration / k;
                double step = (effectiveSourceDuration - clipDur) / (k - 1);

                double currentTimelinePos = 0.0;
                for (int i = 0; i < k; i++)
                {
                    double srcStart = trimStart + (i * step);
                    double srcEnd = srcStart + clipDur;

                    // Snap to nearest detected scene boundary if available (within 1.0s)
                    if (detectedScenes != null && detectedScenes.Count > 0)
                    {
                        var nearestCut = detectedScenes
                            .Select(s => s.StartSeconds)
                            .Where(pt => Math.Abs(pt - srcStart) <= 1.0 && pt >= trimStart && (pt + clipDur) <= (rawVideoDuration - trimEnd + 0.1))
                            .OrderBy(pt => Math.Abs(pt - srcStart))
                            .FirstOrDefault();

                        if (nearestCut > 0.05)
                        {
                            srcStart = nearestCut;
                            srcEnd = srcStart + clipDur;
                        }
                    }

                    var clip = new OneShotClipSegment(i + 1, srcStart, srcEnd);
                    oneShotClips.Add(clip);

                    plan.VideoSlices.Add(new VideoTimelineSlice
                    {
                        VideoFilePath = video.FilePath,
                        SourceStartSeconds = srcStart,
                        SourceDurationSeconds = clipDur,
                        TimelineStartSeconds = currentTimelinePos,
                        LoopIndex = 0
                    });

                    sceneList.Add(new SceneSegment(i + 1, currentTimelinePos, currentTimelinePos + clipDur));
                    currentTimelinePos += clipDur;
                }

                plan.RequiresVideoLooping = false;
                plan.RequiresVideoTrimming = true;
                plan.TotalVideoLoops = 0;
            }
            else if (effectiveSourceDuration < targetDuration)
            {
                // Video is shorter than audio: Loop single video cleanly to fill the timeline
                double currentTimelinePos = 0.0;
                int loopCounter = 0;
                int sIdx = 1;

                while (currentTimelinePos < targetDuration)
                {
                    var remaining = targetDuration - currentTimelinePos;
                    var sliceDur = Math.Min(effectiveSourceDuration, remaining);

                    var clip = new OneShotClipSegment(sIdx, trimStart, trimStart + sliceDur);
                    oneShotClips.Add(clip);

                    plan.VideoSlices.Add(new VideoTimelineSlice
                    {
                        VideoFilePath = video.FilePath,
                        SourceStartSeconds = trimStart,
                        SourceDurationSeconds = sliceDur,
                        TimelineStartSeconds = currentTimelinePos,
                        LoopIndex = loopCounter
                    });

                    sceneList.Add(new SceneSegment(sIdx++, currentTimelinePos, currentTimelinePos + sliceDur));
                    currentTimelinePos += sliceDur;
                    loopCounter++;
                }

                plan.RequiresVideoLooping = true;
                plan.RequiresVideoTrimming = false;
                plan.TotalVideoLoops = loopCounter;
            }
            else
            {
                // Video is roughly equal to audio or SmartCut is off: Single continuous slice
                var clip = new OneShotClipSegment(1, trimStart, trimStart + targetDuration);
                oneShotClips.Add(clip);

                plan.VideoSlices.Add(new VideoTimelineSlice
                {
                    VideoFilePath = video.FilePath,
                    SourceStartSeconds = trimStart,
                    SourceDurationSeconds = targetDuration,
                    TimelineStartSeconds = 0.0,
                    LoopIndex = 0
                });

                // Subdivide into aesthetic rhythmic sub-scenes (3.5s - 4.5s each) for transition placement
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

                plan.RequiresVideoLooping = false;
                plan.RequiresVideoTrimming = (rawVideoDuration - trimStart - trimEnd) > targetDuration;
                plan.TotalVideoLoops = 0;
            }
        }
        else
        {
            // Multi-video fallback
            double currentTimelinePos = 0.0;
            int loopCounter = 0;

            while (currentTimelinePos < targetDuration)
            {
                foreach (var video in validVideos)
                {
                    if (currentTimelinePos >= targetDuration) break;
                    var remaining = targetDuration - currentTimelinePos;
                    var rawDur = video.DurationSeconds > 0 ? video.DurationSeconds : remaining;
                    var sliceDur = Math.Min(rawDur, remaining);

                    plan.VideoSlices.Add(new VideoTimelineSlice
                    {
                        VideoFilePath = video.FilePath,
                        SourceStartSeconds = 0,
                        SourceDurationSeconds = sliceDur,
                        TimelineStartSeconds = currentTimelinePos,
                        LoopIndex = loopCounter
                    });

                    sceneList.Add(new SceneSegment(sceneList.Count + 1, currentTimelinePos, currentTimelinePos + sliceDur));
                    currentTimelinePos += sliceDur;
                }
                loopCounter++;
            }
        }

        plan.OneShotClips = oneShotClips;
        plan.Scenes = sceneList;

        // 2. Plan Transitions (Only on the requested count, uniformly distributed)
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

        plan.SummaryText = $"OneShot Master: {targetDuration:F2}s | Clips: {plan.OneShotClips.Count} | Transitions: {plan.ActiveTransitionsCount}/{reqCount} ({reqType}) | Loops: {plan.TotalVideoLoops}";
        _logger?.LogInformation("Built OneShot timeline plan: {Summary}", plan.SummaryText);

        return plan;
    }
}
