using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

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
        ? $"Scene {FromSceneIndex:D2} → Scene {ToSceneIndex:D2} | {TransitionType} ({DurationSeconds:F2}s) [Score: {CandidateScore:F0}]"
        : $"Scene {FromSceneIndex:D2} → Scene {ToSceneIndex:D2} | CUT";
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
