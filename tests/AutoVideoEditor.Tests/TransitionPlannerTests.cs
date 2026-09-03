using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;
using AutoVideoEditor.VideoEngine;
using Xunit;

namespace AutoVideoEditor.Tests;

public class TransitionPlannerTests
{
    private readonly TransitionPlanner _planner = new();

    private List<SceneSegment> CreateTestScenes(int count, double sceneDuration = 3.0)
    {
        var list = new List<SceneSegment>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new SceneSegment(i + 1, i * sceneDuration, (i + 1) * sceneDuration));
        }
        return list;
    }

    [Fact]
    public void PlanTransitions_10Scenes_Request1_ReturnsExactly1Transition()
    {
        var scenes = CreateTestScenes(10, 3.0); // 30s total
        var transitions = _planner.PlanTransitions(scenes, 1, TransitionType.Smart);

        Assert.Equal(9, transitions.Count); // 9 cut points
        Assert.Single(transitions.Where(t => t.IsActiveTransition));
        Assert.Equal(8, transitions.Count(t => !t.IsActiveTransition && t.TransitionType == TransitionType.Cut));
    }

    [Fact]
    public void PlanTransitions_10Scenes_Request2_ReturnsExactly2Transitions()
    {
        var scenes = CreateTestScenes(10, 3.0);
        var transitions = _planner.PlanTransitions(scenes, 2, TransitionType.Smart);

        Assert.Equal(9, transitions.Count);
        Assert.Equal(2, transitions.Count(t => t.IsActiveTransition));
        Assert.Equal(7, transitions.Count(t => !t.IsActiveTransition));
    }

    [Fact]
    public void PlanTransitions_10Scenes_Request3_ReturnsExactly3Transitions()
    {
        var scenes = CreateTestScenes(10, 3.0);
        var transitions = _planner.PlanTransitions(scenes, 3, TransitionType.Smart);

        Assert.Equal(9, transitions.Count);
        Assert.Equal(3, transitions.Count(t => t.IsActiveTransition));
        Assert.Equal(6, transitions.Count(t => !t.IsActiveTransition));

        // Verify active transitions are spaced out nicely
        var activeOffsets = transitions.Where(t => t.IsActiveTransition).Select(t => t.TimelineOffsetSeconds).ToList();
        for (int i = 0; i < activeOffsets.Count - 1; i++)
        {
            Assert.True(activeOffsets[i + 1] - activeOffsets[i] >= 2.0);
        }
    }

    [Fact]
    public void PlanTransitions_5Scenes_Request10_ClampsToMaxValidPoints()
    {
        var scenes = CreateTestScenes(5, 3.0); // 5 scenes = 4 cut points
        var transitions = _planner.PlanTransitions(scenes, 10, TransitionType.Smart);

        Assert.Equal(4, transitions.Count);
        Assert.True(transitions.Count(t => t.IsActiveTransition) <= 4);
    }

    [Fact]
    public void PlanTransitions_2Scenes_Request10_ReturnsMax1Transition()
    {
        var scenes = CreateTestScenes(2, 4.0); // 2 scenes = 1 cut point
        var transitions = _planner.PlanTransitions(scenes, 10, TransitionType.Smart);

        Assert.Single(transitions);
        Assert.True(transitions.Count(t => t.IsActiveTransition) <= 1);
    }

    [Fact]
    public void PlanTransitions_1Scene_Request10_Returns0Transitions()
    {
        var scenes = CreateTestScenes(1, 10.0);
        var transitions = _planner.PlanTransitions(scenes, 10, TransitionType.Smart);

        Assert.Empty(transitions);
    }

    [Fact]
    public void PlanTransitions_Request0_ReturnsAllCuts()
    {
        var scenes = CreateTestScenes(6, 3.0);
        var transitions = _planner.PlanTransitions(scenes, 0, TransitionType.Smart);

        Assert.Equal(5, transitions.Count);
        Assert.All(transitions, t => Assert.False(t.IsActiveTransition));
        Assert.All(transitions, t => Assert.Equal(TransitionType.Cut, t.TransitionType));
    }

    [Fact]
    public void PlanTransitions_RandomType_AvoidsConsecutiveDuplicateTypes()
    {
        var scenes = CreateTestScenes(12, 3.0);
        var transitions = _planner.PlanTransitions(scenes, 5, TransitionType.Random);

        var activeTransitions = transitions.Where(t => t.IsActiveTransition).ToList();
        Assert.Equal(5, activeTransitions.Count);

        for (int i = 0; i < activeTransitions.Count - 1; i++)
        {
            Assert.NotEqual(activeTransitions[i].TransitionType, activeTransitions[i + 1].TransitionType);
        }
    }

    [Fact]
    public void FilterGraphBuilder_WithActiveTransitions_GeneratesXfadeCommand()
    {
        var job = new VideoJob
        {
            VideoPaths = new List<string> { "C:/test/sample.mp4" },
            VoicePath = "C:/test/sample.mp3"
        };
        job.VideoMetadatas.Add(new MediaFileInfo { FilePath = "C:/test/sample.mp4", DurationSeconds = 30.0, Width = 1080, Height = 1920 });

        var scenes = CreateTestScenes(4, 5.0);
        var transitions = _planner.PlanTransitions(scenes, 2, TransitionType.Smart);

        var plan = new TimelinePlan
        {
            TargetMasterDurationSeconds = 20.0,
            Scenes = scenes,
            Transitions = transitions,
            AudioSpeechSegments = new List<SpeechSegment> { new SpeechSegment(1, 0, 20.0) }
        };

        var preset = ExportPreset.GetDefaultPresets()[0];
        var (args, filter) = FilterGraphBuilder.BuildRenderCommand(job, plan, preset, "h264_nvenc", "C:/test/out.mp4");

        Assert.NotNull(filter);
        Assert.Contains("xfade=", filter);
        Assert.Contains("-movflags +faststart", args);
    }
}
