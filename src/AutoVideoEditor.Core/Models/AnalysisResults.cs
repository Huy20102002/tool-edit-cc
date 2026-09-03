namespace AutoVideoEditor.Core.Models;

public class AudioAnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public double OriginalDurationSeconds { get; set; }
    public double ProcessedDurationSeconds { get; set; }
    public double SilenceDurationRemovedSeconds => Math.Max(0, OriginalDurationSeconds - ProcessedDurationSeconds);
    public double SilenceRemovalPercentage => OriginalDurationSeconds > 0 
        ? (SilenceDurationRemovedSeconds / OriginalDurationSeconds) * 100.0 
        : 0;

    public List<SpeechSegment> SpeechSegments { get; set; } = new();
    public List<SilenceSegment> SilenceSegments { get; set; } = new();
    public float[] WaveformPoints { get; set; } = Array.Empty<float>();
    
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
    public double SilenceThresholdDb { get; set; }
    public int MinSilenceDurationMs { get; set; }
    public int PaddingBeforeMs { get; set; }
    public int PaddingAfterMs { get; set; }
}

public class SceneSegment
{
    public int Index { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);
    public double Score { get; set; }

    public SceneSegment() { }

    public SceneSegment(int index, double start, double end, double score = 0)
    {
        Index = index;
        StartSeconds = start;
        EndSeconds = end;
        Score = score;
    }
}

public class VideoAnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Fps { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public List<SceneSegment> SceneSegments { get; set; } = new();
}
