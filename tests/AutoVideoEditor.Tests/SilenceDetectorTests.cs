using AutoVideoEditor.AudioEngine;
using AutoVideoEditor.Core.Models;
using Xunit;

namespace AutoVideoEditor.Tests;

public class SilenceDetectorTests
{
    [Fact]
    public void CalculateRawSpeechIntervals_WithLeadingAndTrailingSilence_ReturnsCorrectIntervals()
    {
        // 0.0 - 2.0 silence
        // 2.0 - 6.0 speech
        // 6.0 - 8.0 silence
        // 8.0 - 12.0 speech
        // 12.0 - 15.0 silence (total duration 15.0s)
        var silences = new List<(double Start, double End)>
        {
            (0.0, 2.0),
            (6.0, 8.0),
            (12.0, 15.0)
        };

        var rawSpeech = SilenceDetector.CalculateRawSpeechIntervals(silences, 15.0);

        Assert.Equal(2, rawSpeech.Count);
        Assert.Equal(2.0, rawSpeech[0].Start, 2);
        Assert.Equal(6.0, rawSpeech[0].End, 2);
        Assert.Equal(8.0, rawSpeech[1].Start, 2);
        Assert.Equal(12.0, rawSpeech[1].End, 2);
    }

    [Fact]
    public void CalculateRawSpeechIntervals_WithNoSilence_ReturnsFullDuration()
    {
        var silences = new List<(double Start, double End)>();
        var rawSpeech = SilenceDetector.CalculateRawSpeechIntervals(silences, 25.0);

        Assert.Single(rawSpeech);
        Assert.Equal(0.0, rawSpeech[0].Start);
        Assert.Equal(25.0, rawSpeech[0].End);
    }

    [Fact]
    public void ApplyPaddingAndMerge_ExpandsBoundariesAndMergesOverlaps()
    {
        // Speech 1: 2.0 to 4.0
        // Speech 2: 4.1 to 6.0 (gap is only 0.1s)
        // With paddingBefore = 0.08s and paddingAfter = 0.08s:
        // Speech 1 becomes: [1.92, 4.08]
        // Speech 2 becomes: [4.02, 6.08]
        // Since 4.02 <= 4.08, they should merge into [1.92, 6.08]
        var rawSpeech = new List<(double Start, double End)>
        {
            (2.0, 4.0),
            (4.1, 6.0)
        };

        var padded = SilenceDetector.ApplyPaddingAndMerge(rawSpeech, 10.0, 0.08, 0.08);

        Assert.Single(padded);
        Assert.Equal(1.92, padded[0].StartSeconds, 2);
        Assert.Equal(6.08, padded[0].EndSeconds, 2);
        Assert.Equal(4.16, padded[0].DurationSeconds, 2);
    }

    [Fact]
    public void ApplyPaddingAndMerge_ClampsToZeroAndTotalDuration()
    {
        var rawSpeech = new List<(double Start, double End)>
        {
            (0.02, 9.95)
        };

        var padded = SilenceDetector.ApplyPaddingAndMerge(rawSpeech, 10.0, 0.1, 0.1);

        Assert.Single(padded);
        Assert.Equal(0.0, padded[0].StartSeconds);
        Assert.Equal(10.0, padded[0].EndSeconds);
        Assert.Equal(10.0, padded[0].DurationSeconds);
    }
}
