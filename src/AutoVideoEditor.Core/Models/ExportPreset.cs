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
    public int Fps { get; set; } = 60;
    public VideoCodecType VideoCodec { get; set; } = VideoCodecType.H264;
    public VideoBitrateMode BitrateMode { get; set; } = VideoBitrateMode.Auto;
    public int CustomVideoBitrateKbps { get; set; } = 20000;
    public CropMode CropMode { get; set; } = CropMode.FitWithBlur;

    // OneShot Smart Cut & Trimming
    public bool EnableSmartCut { get; set; } = true; // Tự động cắt nhịp OneShot
    public double VideoTrimStartSeconds { get; set; } = 0.0;
    public double VideoTrimEndSeconds { get; set; } = 0.0;
    public double VoiceTrimStartSeconds { get; set; } = 0.0;
    public double VoiceTrimEndSeconds { get; set; } = 0.0;
    public double ExtraEndPaddingSeconds { get; set; } = 0.0; // Dư cuối video (giây)

    // Audio Configuration
    public AudioCodecType AudioCodec { get; set; } = AudioCodecType.AAC;
    public int AudioBitrateKbps { get; set; } = 256;
    public int AudioSampleRate { get; set; } = 48000;
    public bool NormalizeAudio { get; set; } = true;
    public double TargetLufs { get; set; } = -14.0;

    // Silence Detection Settings
    public double SilenceThresholdDb { get; set; } = -35.0;
    public int MinSilenceDurationMs { get; set; } = 400;
    public int PaddingBeforeMs { get; set; } = 80;
    public int PaddingAfterMs { get; set; } = 80;

    // Transitions Configuration (Chuyển cảnh)
    public bool EnableTransitions { get; set; } = true;
    public int TransitionCount { get; set; } = 2; // Số lượng chuyển cảnh mặc định
    public TransitionType TransitionType { get; set; } = TransitionType.Smart; // Kiểu chuyển cảnh
    public double TransitionDurationSeconds { get; set; } = 0.20; // Thời lượng chuyển cảnh (giây)
    public double MinTransitionSpacingSeconds { get; set; } = 2.0; // Khoảng cách tối thiểu giữa 2 chuyển cảnh (giây)

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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "TikTok OneShot (9:16) — Khuyên Dùng",
                Description = "Chuẩn 1080x1920, 60 FPS, 20.000 Kbps, Smart Cut nhịp điệu, Voice Master Timeline",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 60,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 20000,
                EnableSmartCut = true,
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
                EnableTransitions = true,
                TransitionCount = 2,
                TransitionType = TransitionType.Smart,
                TransitionDurationSeconds = 0.20,
                MinTransitionSpacingSeconds = 2.0,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "TikTok / Reels 1080p 30 FPS (9:16)",
                Description = "Độ phân giải 1080x1920, 30 FPS, H.264 15.000 Kbps, Nền mờ tự động",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 30,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 15000,
                EnableSmartCut = true,
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
                EnableTransitions = true,
                TransitionCount = 2,
                TransitionType = TransitionType.Smart,
                TransitionDurationSeconds = 0.20,
                MinTransitionSpacingSeconds = 2.0,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "TikTok / Reels 60 FPS (9:16)",
                Description = "Độ phân giải 1080x1920, 60 FPS siêu mượt, 20.000 Kbps sắc nét chuẩn CapCut",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 60,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 20000,
                EnableSmartCut = true,
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
                EnableTransitions = true,
                TransitionCount = 3,
                TransitionType = TransitionType.Smart,
                TransitionDurationSeconds = 0.20,
                MinTransitionSpacingSeconds = 2.0,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "YouTube Shorts (9:16)",
                Description = "Độ phân giải 1080x1920, 60 FPS, H.264, Âm thanh -14 LUFS",
                IsBuiltIn = true,
                ResolutionWidth = 1080,
                ResolutionHeight = 1920,
                AspectRatio = AspectRatioMode.Ratio9x16,
                Fps = 60,
                CropMode = CropMode.FitWithBlur,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 20000,
                EnableSmartCut = true,
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
                EnableTransitions = true,
                TransitionCount = 2,
                TransitionType = TransitionType.Smart,
                TransitionDurationSeconds = 0.20,
                MinTransitionSpacingSeconds = 2.0,
                HardwareEncoder = HardwareEncoderType.Auto
            },
            new ExportPreset
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "YouTube Standard 1080p (16:9)",
                Description = "Độ phân giải 1920x1080, 60 FPS, Khung hình ngang chuẩn YouTube",
                IsBuiltIn = true,
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                AspectRatio = AspectRatioMode.Ratio16x9,
                Fps = 60,
                CropMode = CropMode.CenterCrop,
                VideoCodec = VideoCodecType.H264,
                BitrateMode = VideoBitrateMode.Auto,
                CustomVideoBitrateKbps = 20000,
                EnableSmartCut = true,
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
                EnableTransitions = true,
                TransitionCount = 2,
                TransitionType = TransitionType.Smart,
                TransitionDurationSeconds = 0.20,
                MinTransitionSpacingSeconds = 2.0,
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
            EnableSmartCut = EnableSmartCut,
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
            EnableTransitions = EnableTransitions,
            TransitionCount = TransitionCount,
            TransitionType = TransitionType,
            TransitionDurationSeconds = TransitionDurationSeconds,
            MinTransitionSpacingSeconds = MinTransitionSpacingSeconds,
            HardwareEncoder = HardwareEncoder,
            EnableSmartSceneCut = EnableSmartSceneCut,
            CreatedAt = DateTime.UtcNow
        };
    }
}
