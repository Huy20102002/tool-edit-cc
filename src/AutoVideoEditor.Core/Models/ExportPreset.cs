using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class ExportPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    // Video Configuration
    public int ResolutionWidth { get; set; } = 1080;
    public int ResolutionHeight { get; set; } = 1920;
    public AspectRatioMode AspectRatio { get; set; } = AspectRatioMode.Ratio9x16;
    public int Fps { get; set; } = 30;
    public VideoCodecType VideoCodec { get; set; } = VideoCodecType.H264;
    public VideoBitrateMode BitrateMode { get; set; } = VideoBitrateMode.Auto;
    public int CustomVideoBitrateKbps { get; set; } = 8000;
    public CropMode CropMode { get; set; } = CropMode.FitWithBlur;

    // Video & Voice Trimming (Cắt đầu / cắt đuôi & Dư cuối video)
    public double VideoTrimStartSeconds { get; set; } = 0.0;
    public double VideoTrimEndSeconds { get; set; } = 0.0;
    public double VoiceTrimStartSeconds { get; set; } = 0.0;
    public double VoiceTrimEndSeconds { get; set; } = 0.0;
    public double ExtraEndPaddingSeconds { get; set; } = 0.0; // Dư cuối video (giây)

    // Audio Configuration
    public AudioCodecType AudioCodec { get; set; } = AudioCodecType.AAC;
    public int AudioBitrateKbps { get; set; } = 192;
    public int AudioSampleRate { get; set; } = 44100;
    public bool NormalizeAudio { get; set; } = true;
    public double TargetLufs { get; set; } = -14.0;

    // Silence Detection Settings
    public double SilenceThresholdDb { get; set; } = -35.0;
    public int MinSilenceDurationMs { get; set; } = 400;
    public int PaddingBeforeMs { get; set; } = 80;
    public int PaddingAfterMs { get; set; } = 80;

    // Performance & Hardware
    public HardwareEncoderType HardwareEncoder { get; set; } = HardwareEncoderType.Auto;
    public bool EnableSmartSceneCut { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static List<ExportPreset> GetDefaultPresets()
    {
        return new List<ExportPreset>
        {
            new ExportPreset
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "TikTok / Reels 1080p (9:16)",
                Description = "Độ phân giải 1080x1920, 30 FPS, H.264, Nền mờ tự động, Chuẩn hóa âm thanh -14 LUFS",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 30,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 8000,
                VideoTrimStartSeconds = 0.0,
                VideoTrimEndSeconds = 0.0,
                VoiceTrimStartSeconds = 0.0,
                VoiceTrimEndSeconds = 0.0,
                AudioCodec = AudioCodecType.AAC,
                AudioBitrateKbps = 192,
                AudioSampleRate = 44100,
                NormalizeAudio = true,
                TargetLufs = -14.0,
                SilenceThresholdDb = -35.0,
                MinSilenceDurationMs = 400,
                PaddingBeforeMs = 80,
                PaddingAfterMs = 80,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "TikTok / Reels 60 FPS (9:16)",
                Description = "Độ phân giải 1080x1920, 60 FPS mượt mà, H.264, Nền mờ tự động",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 60,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 12000,
                VideoTrimStartSeconds = 0.0,
                VideoTrimEndSeconds = 0.0,
                VoiceTrimStartSeconds = 0.0,
                VoiceTrimEndSeconds = 0.0,
                AudioCodec = AudioCodecType.AAC,
                AudioBitrateKbps = 256,
                AudioSampleRate = 48000,
                NormalizeAudio = true,
                TargetLufs = -14.0,
                SilenceThresholdDb = -35.0,
                MinSilenceDurationMs = 400,
                PaddingBeforeMs = 80,
                PaddingAfterMs = 80,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "YouTube Shorts (9:16)",
                Description = "Độ phân giải 1080x1920, 30 FPS, H.264, Âm thanh -14 LUFS",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 30,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 8000,
                VideoTrimStartSeconds = 0.0,
                VideoTrimEndSeconds = 0.0,
                VoiceTrimStartSeconds = 0.0,
                VoiceTrimEndSeconds = 0.0,
                AudioCodec = AudioCodecType.AAC,
                AudioBitrateKbps = 192,
                AudioSampleRate = 44100,
                NormalizeAudio = true,
                TargetLufs = -14.0,
                SilenceThresholdDb = -35.0,
                MinSilenceDurationMs = 400,
                PaddingBeforeMs = 80,
                PaddingAfterMs = 80,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "YouTube Standard 1080p (16:9)",
                Description = "Độ phân giải 1920x1080 ngang, 30 FPS, H.264, Cắt giữa hoặc Vừa khung",
                IsBuiltIn = true,
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                AspectRatio = AspectRatioMode.Ratio16x9,
                Fps = 30,
                CropMode = CropMode.FitBlackBars,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 10000,
                VideoTrimStartSeconds = 0.0,
                VideoTrimEndSeconds = 0.0,
                VoiceTrimStartSeconds = 0.0,
                VoiceTrimEndSeconds = 0.0,
                AudioCodec = AudioCodecType.AAC,
                AudioBitrateKbps = 192,
                AudioSampleRate = 44100,
                NormalizeAudio = true,
                TargetLufs = -14.0,
                SilenceThresholdDb = -35.0,
                MinSilenceDurationMs = 400,
                PaddingBeforeMs = 80,
                PaddingAfterMs = 80,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Facebook / Instagram Feed (4:5)",
                Description = "Độ phân giải 1080x1350, tỷ lệ 4:5 tối ưu cho newfeed di động",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1350,
                AspectRatio = AspectRatioMode.Ratio4x5,
                Fps = 30,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 8000,
                VideoTrimStartSeconds = 0.0,
                VideoTrimEndSeconds = 0.0,
                VoiceTrimStartSeconds = 0.0,
                VoiceTrimEndSeconds = 0.0,
                AudioCodec = AudioCodecType.AAC,
                AudioBitrateKbps = 192,
                AudioSampleRate = 44100,
                NormalizeAudio = true,
                TargetLufs = -14.0,
                SilenceThresholdDb = -35.0,
                MinSilenceDurationMs = 400,
                PaddingBeforeMs = 80,
                PaddingAfterMs = 80,
                HardwareEncoder = HardwareEncoderType.Auto
            }
        };
    }

    public ExportPreset Clone(string? newName = null)
    {
        return new ExportPreset
        {
            Id = Guid.NewGuid(),
            Name = newName ?? $"{Name} (Bản sao)",
            Description = Description,
            IsBuiltIn = false,
            ResolutionWidth = ResolutionWidth,
            ResolutionHeight = ResolutionHeight,
            AspectRatio = AspectRatio,
            Fps = Fps,
            VideoCodec = VideoCodec,
            BitrateMode = BitrateMode,
            CustomVideoBitrateKbps = CustomVideoBitrateKbps,
            CropMode = CropMode,
            VideoTrimStartSeconds = VideoTrimStartSeconds,
            VideoTrimEndSeconds = VideoTrimEndSeconds,
            VoiceTrimStartSeconds = VoiceTrimStartSeconds,
            VoiceTrimEndSeconds = VoiceTrimEndSeconds,
            ExtraEndPaddingSeconds = ExtraEndPaddingSeconds,
            AudioCodec = AudioCodec,
            AudioBitrateKbps = AudioBitrateKbps,
            AudioSampleRate = AudioSampleRate,
            NormalizeAudio = NormalizeAudio,
            TargetLufs = TargetLufs,
            SilenceThresholdDb = SilenceThresholdDb,
            MinSilenceDurationMs = MinSilenceDurationMs,
            PaddingBeforeMs = PaddingBeforeMs,
            PaddingAfterMs = PaddingAfterMs,
            HardwareEncoder = HardwareEncoder,
            EnableSmartSceneCut = EnableSmartSceneCut,
            CreatedAt = DateTime.UtcNow
        };
    }
}
