using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class AppSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "AutoVideoEditor_Output"
    );

    public string TempDirectory { get; set; } = Path.Combine(
        Path.GetTempPath(),
        "AutoVideoEditor"
    );

    public int MaxParallelJobs { get; set; } = 2;
    public HardwareEncoderType HardwareEncoderPreference { get; set; } = HardwareEncoderType.Auto;
    public Guid DefaultPresetId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public OverwritePolicy OverwritePolicy { get; set; } = OverwritePolicy.AutoRename;
    public string OutputNamingPattern { get; set; } = "{original_name}_edited";

    // Silence detection defaults
    public double DefaultSilenceThresholdDb { get; set; } = -35.0;
    public int DefaultMinSilenceMs { get; set; } = 400;
    public int DefaultPaddingBeforeMs { get; set; } = 80;
    public int DefaultPaddingAfterMs { get; set; } = 80;
    public double DefaultTargetLufs { get; set; } = -14.0;

    // UI & Localization
    public string Language { get; set; } = "vi-VN";
    public string Theme { get; set; } = "Dark";
    public bool AutoScrollQueue { get; set; } = true;
    public bool AutoCleanTempFiles { get; set; } = true;
    public string CustomFFmpegPath { get; set; } = string.Empty;
    public string CustomFFprobePath { get; set; } = string.Empty;
}
