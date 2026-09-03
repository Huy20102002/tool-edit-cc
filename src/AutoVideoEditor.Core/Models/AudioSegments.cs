namespace AutoVideoEditor.Core.Models;

public class SpeechSegment
{
    public int Index { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);

    public SpeechSegment() { }

    public SpeechSegment(int index, double start, double end)
    {
        Index = index;
        StartSeconds = start;
        EndSeconds = end;
    }

    public override string ToString() => $"[Speech #{Index}: {StartSeconds:F2}s -> {EndSeconds:F2}s ({DurationSeconds:F2}s)]";
}

public class SilenceSegment
{
    public int Index { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);

    public SilenceSegment() { }

    public SilenceSegment(int index, double start, double end)
    {
        Index = index;
        StartSeconds = start;
        EndSeconds = end;
    }

    public override string ToString() => $"[Silence #{Index}: {StartSeconds:F2}s -> {EndSeconds:F2}s ({DurationSeconds:F2}s)]";
}
