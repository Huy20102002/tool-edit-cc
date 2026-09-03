using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class TransitionPlanner : ITransitionPlanner
{
    private readonly ILogger<TransitionPlanner>? _logger;
    private static readonly Random Rng = new();

    private static readonly TransitionType[] SmartSequence = 
    {
        TransitionType.Dissolve,
        TransitionType.Zoom,
        TransitionType.Fade,
        TransitionType.Slide,
        TransitionType.Wipe
    };

    public TransitionPlanner(ILogger<TransitionPlanner>? logger = null)
    {
        _logger = logger;
    }

    public List<TransitionPlanItem> PlanTransitions(
        IReadOnlyList<SceneSegment> scenes,
        int requestedTransitionCount,
        TransitionType requestedType,
        double defaultDurationSeconds = 0.20,
        double minSpacingSeconds = 2.0,
        IReadOnlyList<SpeechSegment>? speechSegments = null)
    {
        var result = new List<TransitionPlanItem>();
        if (scenes == null || scenes.Count <= 1)
        {
            return result;
        }

        int totalPoints = scenes.Count - 1;
        double totalDuration = scenes.Sum(s => s.DurationSeconds);
        if (totalDuration <= 0) totalDuration = 10.0;

        // Build all available cut points
        var allPoints = new List<TransitionCandidateScore>();
        double currentPos = 0.0;

        for (int i = 0; i < totalPoints; i++)
        {
            var fromScene = scenes[i];
            var toScene = scenes[i + 1];
            currentPos += fromScene.DurationSeconds;

            allPoints.Add(new TransitionCandidateScore
            {
                PointIndex = i + 1,
                FromSceneIndex = fromScene.Index,
                ToSceneIndex = toScene.Index,
                TimelinePosition = currentPos,
                FromSceneDuration = fromScene.DurationSeconds,
                ToSceneDuration = toScene.DurationSeconds,
                Score = 0.0
            });
        }

        // If 0 transitions requested or type is None/Cut, return all as CUT
        if (requestedTransitionCount <= 0 || requestedType == TransitionType.None || requestedType == TransitionType.Cut)
        {
            foreach (var pt in allPoints)
            {
                result.Add(new TransitionPlanItem
                {
                    PointIndex = pt.PointIndex,
                    FromSceneIndex = pt.FromSceneIndex,
                    ToSceneIndex = pt.ToSceneIndex,
                    TimelineOffsetSeconds = pt.TimelinePosition,
                    TransitionType = TransitionType.Cut,
                    DurationSeconds = 0,
                    IsActiveTransition = false,
                    CandidateScore = 0
                });
            }
            return result;
        }

        // 1. Filter out unsafe points (too close to start/end, or scenes too short)
        double startMargin = totalDuration >= 6.0 ? 1.5 : 0.5;
        double endMargin = totalDuration >= 6.0 ? 1.5 : 0.5;
        double minSceneDuration = Math.Max(0.3, defaultDurationSeconds * 1.5);

        var validCandidates = allPoints.Where(p =>
            p.TimelinePosition >= startMargin &&
            (totalDuration - p.TimelinePosition) >= endMargin &&
            p.FromSceneDuration >= minSceneDuration &&
            p.ToSceneDuration >= minSceneDuration
        ).ToList();

        // If strict filtering removed all points but we have points, fallback to all points
        if (validCandidates.Count == 0 && allPoints.Count > 0)
        {
            validCandidates = allPoints.Where(p => 
                p.TimelinePosition >= 0.2 && 
                (totalDuration - p.TimelinePosition) >= 0.2
            ).ToList();
            if (validCandidates.Count == 0)
            {
                validCandidates = allPoints.ToList();
            }
        }

        // 2. Clamp requested count to available valid points
        int targetCount = Math.Min(requestedTransitionCount, validCandidates.Count);

        // 3. Calculate ideal split positions on timeline: 1/(K+1), 2/(K+1), ..., K/(K+1)
        var idealPositions = new List<double>();
        for (int k = 1; k <= targetCount; k++)
        {
            idealPositions.Add((double)k / (targetCount + 1) * totalDuration);
        }

        // 4. Score each candidate point
        foreach (var candidate in validCandidates)
        {
            double score = 0.0;

            // Score A: Closeness to ideal timeline fractions (+35 pts max)
            double minDistanceToIdeal = idealPositions.Count > 0 
                ? idealPositions.Min(ideal => Math.Abs(candidate.TimelinePosition - ideal))
                : 0.0;
            double fractionInterval = totalDuration / (targetCount + 1);
            double distanceFactor = Math.Max(0.0, 1.0 - (minDistanceToIdeal / Math.Max(1.0, fractionInterval)));
            score += distanceFactor * 35.0;

            // Score B: Scene Duration (+25 pts max)
            double combinedDur = candidate.FromSceneDuration + candidate.ToSceneDuration;
            score += Math.Min(25.0, combinedDur * 3.0);

            // Score C: Speech pause alignment (+20 pts max)
            if (speechSegments != null && speechSegments.Count > 1)
            {
                bool nearPause = false;
                for (int s = 0; s < speechSegments.Count - 1; s++)
                {
                    double pauseStart = speechSegments[s].EndSeconds;
                    double pauseEnd = speechSegments[s + 1].StartSeconds;
                    if (candidate.TimelinePosition >= pauseStart - 0.3 && candidate.TimelinePosition <= pauseEnd + 0.3)
                    {
                        nearPause = true;
                        break;
                    }
                }
                if (nearPause) score += 20.0;
            }

            // Score D: Base aesthetic placement (+20 pts)
            score += 20.0;

            candidate.Score = score;
        }

        // 5. Select Top-K points respecting MinTransitionSpacingSeconds
        var selectedPoints = new List<TransitionCandidateScore>();
        var sortedCandidates = validCandidates.OrderByDescending(c => c.Score).ToList();

        double effectiveSpacing = minSpacingSeconds;
        int maxAttempts = 3;

        while (selectedPoints.Count < targetCount && maxAttempts-- > 0)
        {
            selectedPoints.Clear();
            foreach (var candidate in sortedCandidates)
            {
                bool tooClose = selectedPoints.Any(sel => Math.Abs(sel.TimelinePosition - candidate.TimelinePosition) < effectiveSpacing);
                if (!tooClose)
                {
                    selectedPoints.Add(candidate);
                    if (selectedPoints.Count == targetCount)
                        break;
                }
            }

            // If spacing was too strict to get targetCount, relax spacing
            effectiveSpacing = Math.Max(0.5, effectiveSpacing * 0.6);
        }

        var selectedPointIndices = new HashSet<int>(selectedPoints.Select(p => p.PointIndex));

        // 6. Assign Transition Types
        TransitionType lastRandomType = TransitionType.None;
        int activeCounter = 0;

        for (int i = 0; i < allPoints.Count; i++)
        {
            var pt = allPoints[i];
            bool isActive = selectedPointIndices.Contains(pt.PointIndex);

            if (isActive)
            {
                TransitionType assignedType;
                if (requestedType == TransitionType.Smart)
                {
                    assignedType = SmartSequence[activeCounter % SmartSequence.Length];
                }
                else if (requestedType == TransitionType.Random)
                {
                    var choices = SmartSequence.Where(t => t != lastRandomType).ToArray();
                    assignedType = choices[Rng.Next(choices.Length)];
                    lastRandomType = assignedType;
                }
                else
                {
                    assignedType = requestedType;
                }

                double safeDur = Math.Min(defaultDurationSeconds, Math.Min(pt.FromSceneDuration, pt.ToSceneDuration) / 2.0);
                safeDur = Math.Max(0.10, Math.Round(safeDur, 2));

                result.Add(new TransitionPlanItem
                {
                    PointIndex = pt.PointIndex,
                    FromSceneIndex = pt.FromSceneIndex,
                    ToSceneIndex = pt.ToSceneIndex,
                    TimelineOffsetSeconds = pt.TimelinePosition,
                    TransitionType = assignedType,
                    DurationSeconds = safeDur,
                    IsActiveTransition = true,
                    CandidateScore = pt.Score
                });

                activeCounter++;
            }
            else
            {
                result.Add(new TransitionPlanItem
                {
                    PointIndex = pt.PointIndex,
                    FromSceneIndex = pt.FromSceneIndex,
                    ToSceneIndex = pt.ToSceneIndex,
                    TimelineOffsetSeconds = pt.TimelinePosition,
                    TransitionType = TransitionType.Cut,
                    DurationSeconds = 0,
                    IsActiveTransition = false,
                    CandidateScore = pt.Score
                });
            }
        }

        return result;
    }
}
