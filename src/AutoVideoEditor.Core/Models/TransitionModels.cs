using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class OneShotClipSegment
{
    public int Index { get; set; }
    public double SourceStartSeconds { get; set; }
    public double SourceEndSeconds { get; set; }
    public double DurationSeconds => Math.Max(0, SourceEndSeconds - SourceStartSeconds);
    public string Description => $"Đoạn {Index:D2}: {SourceStartSeconds:F2}s → {SourceEndSeconds:F2}s ({DurationSeconds:F2}s)";

    public OneShotClipSegment() { }

    public OneShotClipSegment(int index, double start, double end)
    {
        Index = index;
        SourceStartSeconds = start;
        SourceEndSeconds = end;
    }
}

public class TransitionPlanItem
{
    public int PointIndex { get; set; }
    public int FromSceneIndex { get; set; }
    public int ToSceneIndex { get; set; }
    public double TimelineOffsetSeconds { get; set; }
    public TransitionType TransitionType { get; set; } = TransitionType.Smart;
    public double DurationSeconds { get; set; } = 0.20;
    public bool IsActiveTransition { get; set; }
    public double CandidateScore { get; set; }
    public string Description => IsActiveTransition 
        ? $"Đoạn {FromSceneIndex:D2} → Đoạn {ToSceneIndex:D2} | {TransitionType} ({DurationSeconds:F2}s)"
        : $"Đoạn {FromSceneIndex:D2} → Đoạn {ToSceneIndex:D2} | CUT (Jump Cut)";
}

public class TransitionCandidateScore
{
    public int PointIndex { get; set; }
    public int FromSceneIndex { get; set; }
    public int ToSceneIndex { get; set; }
    public double TimelinePosition { get; set; }
    public double FromSceneDuration { get; set; }
    public double ToSceneDuration { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class TransitionTypeOption
{
    public TransitionType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
