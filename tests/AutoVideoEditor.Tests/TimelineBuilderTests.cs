using AutoVideoEditor.Core.Models;
using AutoVideoEditor.VideoEngine;
using Xunit;

namespace AutoVideoEditor.Tests;

public class TimelineBuilderTests
{
    private readonly TimelineBuilder _builder = new();
    private readonly ExportPreset _preset = ExportPreset.GetDefaultPresets()[0];

    [Fact]
    public void BuildTimeline_OneShot_SmartCut_ExtractsJumpCuts()
    {
        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/video45s.mp4", DurationSeconds = 45.0, Width = 1920, Height = 1080 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 35.8,
            ProcessedDurationSeconds = 29.4,
            SpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 10),
                new SpeechSegment(2, 12, 31.4)
            }
        };

        var customPreset = _preset.Clone();
        customPreset.EnableSmartCut = true;

        var plan = _builder.BuildTimeline(videos, voiceAnalysis, customPreset);

        Assert.Equal(29.4, plan.TargetMasterDurationSeconds, 2);
        Assert.True(plan.VideoSlices.Count >= 4);
        Assert.Equal(29.4, plan.VideoSlices.Sum(s => s.SourceDurationSeconds), 2);
        Assert.False(plan.RequiresVideoLooping);
        Assert.True(plan.RequiresVideoTrimming);
    }

    [Fact]
    public void BuildTimeline_OneShot_ContinuousTrim_SingleSlice()
    {
        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/video45s.mp4", DurationSeconds = 45.0, Width = 1920, Height = 1080 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 35.8,
            ProcessedDurationSeconds = 29.4,
            SpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 10),
                new SpeechSegment(2, 12, 31.4)
            }
        };

        var customPreset = _preset.Clone();
        customPreset.EnableSmartCut = false;

        var plan = _builder.BuildTimeline(videos, voiceAnalysis, customPreset);

        Assert.Equal(29.4, plan.TargetMasterDurationSeconds, 2);
        Assert.Single(plan.VideoSlices);
        Assert.Equal(29.4, plan.VideoSlices[0].SourceDurationSeconds, 2);
        Assert.False(plan.RequiresVideoLooping);
    }

    [Fact]
    public void BuildTimeline_VideoShorterThanVoice_LoopsVideoToCoverMasterDuration()
    {
        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/video10s.mp4", DurationSeconds = 10.0, Width = 1920, Height = 1080 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 40.0,
            ProcessedDurationSeconds = 35.0,
            SpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 35.0)
            }
        };

        var plan = _builder.BuildTimeline(videos, voiceAnalysis, _preset);

        Assert.Equal(35.0, plan.TargetMasterDurationSeconds, 2);
        Assert.True(plan.RequiresVideoLooping);
        Assert.Equal(4, plan.VideoSlices.Count); // 10s + 10s + 10s + 5s = 35s
        Assert.Equal(10.0, plan.VideoSlices[0].SourceDurationSeconds);
        Assert.Equal(10.0, plan.VideoSlices[1].SourceDurationSeconds);
        Assert.Equal(10.0, plan.VideoSlices[2].SourceDurationSeconds);
        Assert.Equal(5.0, plan.VideoSlices[3].SourceDurationSeconds);
        Assert.Equal(35.0, plan.VideoSlices.Sum(s => s.SourceDurationSeconds), 2);
    }

    [Fact]
    public void BuildTimeline_MultipleVideos_DistributesAcrossMasterDuration()
    {
        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/clip1.mp4", DurationSeconds = 5.0 },
            new MediaFileInfo { FilePath = "C:/media/clip2.mp4", DurationSeconds = 7.0 },
            new MediaFileInfo { FilePath = "C:/media/clip3.mp4", DurationSeconds = 8.0 }
        }; // Total pool = 20s

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 28.0,
            ProcessedDurationSeconds = 25.0,
            SpeechSegments = new List<SpeechSegment> { new SpeechSegment(1, 0, 25.0) }
        };

        var plan = _builder.BuildTimeline(videos, voiceAnalysis, _preset);

        Assert.Equal(25.0, plan.TargetMasterDurationSeconds, 2);
        Assert.Equal(25.0, plan.VideoSlices.Sum(s => s.SourceDurationSeconds), 2);
        Assert.Equal(4, plan.VideoSlices.Count); // 5s + 7s + 8s (20s) + 5s (25s)
    }

    [Fact]
    public void BuildTimeline_WithVideoTrimStartAndEnd_CalculatesEffectiveDurationCorrectly()
    {
        var customPreset = _preset.Clone("TrimPreset");
        customPreset.VideoTrimStartSeconds = 2.0; // Trim 2s at start
        customPreset.VideoTrimEndSeconds = 3.0;   // Trim 3s at end

        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/video15s.mp4", DurationSeconds = 15.0 } // 15 - 2 - 3 = 10s effective
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 20.0,
            ProcessedDurationSeconds = 20.0,
            SpeechSegments = new List<SpeechSegment> { new SpeechSegment(1, 0, 20.0) }
        };

        var plan = _builder.BuildTimeline(videos, voiceAnalysis, customPreset);

        Assert.Equal(20.0, plan.TargetMasterDurationSeconds, 2);
        Assert.Equal(2, plan.VideoSlices.Count); // 10s + 10s = 20s
        Assert.Equal(2.0, plan.VideoSlices[0].SourceStartSeconds);
        Assert.Equal(10.0, plan.VideoSlices[0].SourceDurationSeconds);
        Assert.Equal(10.0, plan.VideoSlices[1].SourceDurationSeconds);
    }

    [Fact]
    public void BuildTimeline_WithCustomExtraEndPadding_ExtendsTargetDurationCorrectly()
    {
        var videos = new List<MediaFileInfo>
        {
            new MediaFileInfo { FilePath = "C:/media/video20s.mp4", DurationSeconds = 20.0 }
        };

        var voiceAnalysis = new AudioAnalysisResult
        {
            OriginalDurationSeconds = 15.0,
            ProcessedDurationSeconds = 12.0,
            SpeechSegments = new List<SpeechSegment> { new SpeechSegment(1, 0, 12.0) }
        };

        // Add 3.0s extra outro padding
        var plan = _builder.BuildTimeline(videos, voiceAnalysis, _preset, customExtraEnd: 3.0);

        Assert.Equal(15.0, plan.TargetMasterDurationSeconds, 2); // 12.0 + 3.0 = 15.0s
        Assert.Equal(15.0, plan.VideoSlices.Sum(s => s.SourceDurationSeconds), 2);
    }
}
