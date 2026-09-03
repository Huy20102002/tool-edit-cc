using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;
using AutoVideoEditor.VideoEngine;
using Xunit;

namespace AutoVideoEditor.Tests;

public class FilterGraphBuilderTests
{
    [Fact]
    public void BuildRenderCommand_WithFitWithBlur_GeneratesCorrectFilterSyntax()
    {
        var preset = ExportPreset.GetDefaultPresets()[0]; // TikTok 1080p, FitWithBlur
        var job = new VideoJob
        {
            VideoPaths = new List<string> { "C:/input/video.mp4" },
            VoicePath = "C:/input/voice.mp3",
            OutputPath = "C:/output/result.mp4",
            Preset = preset
        };

        var timelinePlan = new TimelinePlan
        {
            TargetMasterDurationSeconds = 18.5,
            AudioSpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 1.0, 10.0),
                new SpeechSegment(2, 11.0, 20.5)
            }
        };

        var (args, filter) = FilterGraphBuilder.BuildRenderCommand(
            job,
            timelinePlan,
            preset,
            "libx264",
            job.OutputPath);

        Assert.NotNull(filter);
        Assert.Contains("boxblur=5:2", filter);
        Assert.Contains("asplit=2", filter);
        Assert.Contains("overlay=(W-w)/2:(H-h)/2", filter);
        Assert.Contains("atrim=start=1.000:end=10.000", filter);
        Assert.Contains("atrim=start=11.000:end=20.500", filter);
        Assert.Contains("concat=n=2:v=0:a=1", filter);
        Assert.Contains("loudnorm=", filter);
        Assert.Contains("-t 18.500", args);
        Assert.Contains("-c:v libx264", args);
    }

    [Fact]
    public void BuildRenderCommand_WithCenterCrop_GeneratesCropFilter()
    {
        var preset = ExportPreset.GetDefaultPresets()[0];
        preset.CropMode = CropMode.CenterCrop;

        var job = new VideoJob
        {
            VideoPaths = new List<string> { "C:/input/video.mp4" },
            VoicePath = "C:/input/voice.mp3",
            OutputPath = "C:/output/result.mp4",
            Preset = preset
        };

        var timelinePlan = new TimelinePlan
        {
            TargetMasterDurationSeconds = 12.0,
            AudioSpeechSegments = new List<SpeechSegment>
            {
                new SpeechSegment(1, 0, 12.0)
            }
        };

        var (args, filter) = FilterGraphBuilder.BuildRenderCommand(
            job,
            timelinePlan,
            preset,
            "h264_nvenc",
            job.OutputPath);

        Assert.NotNull(filter);
        Assert.Contains("scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920", filter);
        Assert.Contains("-c:v h264_nvenc", args);
    }
}
