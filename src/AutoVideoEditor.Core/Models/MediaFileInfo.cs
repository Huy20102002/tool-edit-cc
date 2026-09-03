namespace AutoVideoEditor.Core.Models;

public class MediaFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Fps { get; set; }
    public long BitrateBps { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public int AudioChannels { get; set; }
    public int AudioSampleRate { get; set; }
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; }
    public long FileSizeBytes { get; set; }

    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.Hours > 0 
                ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100}" 
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";
        }
    }

    public string ResolutionString => HasVideo ? $"{Width}x{Height}" : "N/A";
    public string FpsString => HasVideo ? $"{Fps:F1} fps" : "N/A";
    public string SizeString => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
}
