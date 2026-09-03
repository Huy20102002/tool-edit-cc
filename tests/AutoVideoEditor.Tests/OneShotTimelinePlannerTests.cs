using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;
using AutoVideoEditor.VideoEngine;
using Xunit;

namespace AutoVideoEditor.Tests;

public class OneShotTimelinePlannerTests
{
    [Fact]
    public void BuildTimeline_OneShot_Video60s_Audio30s_ExtractsRhythmicJumpCuts()
    {
        var builder = new TimelineBuilder();
        var videoMetas = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/videos/oneshot_60s.mp4", DurationSeconds = 60.0, Width = 1080, Height = 1920 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 35.0,
            ProcessedDurationSeconds = 30.0,
            SpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 30.0)
            }
        };

        var preset = ExportPreset.GetDefaultPresets()[0]; // TikTok OneShot
        preset.EnableSmartCut = true;
        preset.TransitionCount = 3;

        var plan = builder.BuildTimeline(videoMetas, voiceAnalysis, preset);

        Assert.NotNull(plan);
        Assert.Equal(30.0, plan.TargetMasterDurationSeconds, precision: 2);
        Assert.True(plan.OneShotClips.Count >= 4, $"Expected >= 4 clips, got {plan.OneShotClips.Count}");
        
        // Sum of all slice durations should equal 30.0s
        var totalVideoDuration = plan.VideoSlices.Sum(s => s.SourceDurationSeconds);
        Assert.Equal(30.0, totalVideoDuration, precision: 2);

        // Clips must span across the 60s source
        var firstClipStart = plan.OneShotClips.First().SourceStartSeconds;
        var lastClipEnd = plan.OneShotClips.Last().SourceEndSeconds;
        Assert.True(firstClipStart >= 0.0);
        Assert.True(lastClipEnd > 45.0, $"Expected last clip to reach near end of 60s video, but was {lastClipEnd}");

        // Transitions: exactly 3 active transitions
        Assert.Equal(3, plan.ActiveTransitionsCount);
    }

    [Fact]
    public void BuildTimeline_OneShot_VideoShorterThanAudio_LoopsCorrectly()
    {
        var builder = new TimelineBuilder();
        var videoMetas = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/videos/oneshot_15s.mp4", DurationSeconds = 15.0, Width = 1080, Height = 1920 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 30.0,
            ProcessedDurationSeconds = 30.0,
            SpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 30.0)
            }
        };

        var preset = ExportPreset.GetDefaultPresets()[0];
        var plan = builder.BuildTimeline(videoMetas, voiceAnalysis, preset);

        Assert.NotNull(plan);
        Assert.True(plan.RequiresVideoLooping);
        Assert.Equal(30.0, plan.TargetMasterDurationSeconds, precision: 2);
        Assert.Equal(30.0, plan.VideoSlices.Sum(s => s.SourceDurationSeconds), precision: 2);
        Assert.True(plan.TotalVideoLoops >= 2);
    }

    [Fact]
    public void BuildTimeline_OneShot_ZeroTransitions_AllCutsArePureJumpCuts()
    {
        var builder = new TimelineBuilder();
        var videoMetas = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/videos/oneshot_60s.mp4", DurationSeconds = 60.0, Width = 1080, Height = 1920 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 30.0,
            ProcessedDurationSeconds = 30.0,
            SpeechSegments = new List<SpeechSegment> { new SpeechSegment(1, 0, 30.0) }
        };

        var preset = ExportPreset.GetDefaultPresets()[0];
        preset.TransitionCount = 0;

        var plan = builder.BuildTimeline(videoMetas, voiceAnalysis, preset, customTransitionCount: 0);

        Assert.Equal(0, plan.ActiveTransitionsCount);
        Assert.All(plan.Transitions, t => Assert.False(t.IsActiveTransition));
    }
}
