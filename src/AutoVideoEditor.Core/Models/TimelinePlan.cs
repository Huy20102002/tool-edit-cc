namespace AutoVideoEditor.Core.Models;

public class VideoTimelineSlice
{
    public string VideoFilePath { get; set; } = string.Empty;
    public double SourceStartSeconds { get; set; }
    public double SourceDurationSeconds { get; set; }
    public double TimelineStartSeconds { get; set; }
    public double TimelineDurationSeconds => SourceDurationSeconds;
    public int LoopIndex { get; set; }
}

public class TimelinePlan
{
    public double TargetMasterDurationSeconds { get; set; }
    public List<VideoTimelineSlice> VideoSlices { get; set; } = new();
    public List<SpeechSegment> AudioSpeechSegments { get; set; } = new();
    public bool RequiresVideoLooping { get; set; }
    public bool RequiresVideoTrimming { get; set; }
    public int TotalVideoLoops { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}
