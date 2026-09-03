using AutoVideoEditor.AudioEngine;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;
using AutoVideoEditor.Infrastructure.FFmpeg;
using AutoVideoEditor.Infrastructure.Hardware;
using AutoVideoEditor.Infrastructure.Services;
using AutoVideoEditor.VideoEngine;
using Xunit;

namespace AutoVideoEditor.Tests;

public class EndToEndProcessingTests
{
    [Fact]
    public async Task EndToEnd_SilenceDetection_And_VideoRender_GeneratesPlayableMP4()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "AutoVideoEditor_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);

        var testVideo = Path.Combine(testDir, "test_input_video.mp4");
        var testVoice = Path.Combine(testDir, "test_input_voice.wav");
        var testOutput = Path.Combine(testDir, "test_output_1080p.mp4");

        try
        {
            var settingsService = new SettingsService();
            var locator = new FFmpegLocator(settingsService);
            var runner = new FFmpegProcessRunner();
            var probeService = new FFprobeService(locator, runner);
            var hardwareDetector = new HardwareDetector(locator, runner);
            var silenceDetector = new SilenceDetector(locator, runner, probeService);
            var waveformGen = new WaveformGenerator(locator);
            var audioAnalyzer = new AudioAnalyzer(silenceDetector, waveformGen);
            var timelineBuilder = new TimelineBuilder();
            var videoRenderer = new VideoRenderer(locator, runner, hardwareDetector);

            var ffmpegPath = locator.GetFFmpegPath();

            // 1. Generate synthetic test video: 3 seconds 1280x720 30fps
            var genVideoArgs = $"-f lavfi -i testsrc=duration=3:size=1280x720:rate=30 -pix_fmt yuv420p -y \"{testVideo}\"";
            var vExit = await runner.ExecuteAsync(ffmpegPath, genVideoArgs);
            Assert.Equal(0, vExit);

            // 2. Generate synthetic test audio:
            // 1.5s silence + 2s beep (440Hz) + 1.5s silence + 2s beep (880Hz) + 1s silence = 8s total
            var genAudioArgs = $"-f lavfi -i \"aevalsrc=0:d=1.5\" -f lavfi -i \"sine=frequency=440:duration=2\" -f lavfi -i \"aevalsrc=0:d=1.5\" -f lavfi -i \"sine=frequency=880:duration=2\" -f lavfi -i \"aevalsrc=0:d=1\" -filter_complex \"[0:a][1:a][2:a][3:a][4:a]concat=n=5:v=0:a=1[a]\" -map \"[a]\" -y \"{testVoice}\"";
            var aExit = await runner.ExecuteAsync(ffmpegPath, genAudioArgs);
            Assert.Equal(0, aExit);

            // 3. Run Audio Analyzer (Silence Detection)
            var voiceAnalysis = await audioAnalyzer.AnalyzeVoiceAsync(
                testVoice,
                silenceThresholdDb: -30.0,
                minSilenceDurationMs: 400,
                paddingBeforeMs: 50,
                paddingAfterMs: 50);

            Assert.True(voiceAnalysis.OriginalDurationSeconds >= 7.5);
            Assert.True(voiceAnalysis.ProcessedDurationSeconds < 5.5); // ~4.2s speech after cut
            Assert.True(voiceAnalysis.SilenceDurationRemovedSeconds >= 3.0);
            Assert.NotEmpty(voiceAnalysis.SpeechSegments);
            Assert.NotEmpty(voiceAnalysis.WaveformPoints);

            // 4. Probe video
            var videoInfo = await probeService.ProbeFileAsync(testVideo);
            Assert.True(videoInfo.HasVideo);
            Assert.Equal(1280, videoInfo.Width);
            Assert.Equal(720, videoInfo.Height);

            // 5. Build Timeline (Video is 3s, Voice is ~4.2s, so video must loop)
            var preset = ExportPreset.GetDefaultPresets()[0]; // TikTok 1080x1920 9:16 FitWithBlur
            var timelinePlan = timelineBuilder.BuildTimeline(new[] { videoInfo }, voiceAnalysis, preset);

            Assert.True(timelinePlan.RequiresVideoLooping);
            Assert.Equal(voiceAnalysis.ProcessedDurationSeconds, timelinePlan.TargetMasterDurationSeconds, 2);

            // 6. Render Video Job
            var job = new VideoJob
            {
                VideoPaths = new List<string> { testVideo },
                VoicePath = testVoice,
                OutputPath = testOutput,
                Preset = preset,
                TimelinePlan = timelinePlan
            };

            var reportedProgress = new List<double>();
            await videoRenderer.RenderAsync(job, timelinePlan, report =>
            {
                reportedProgress.Add(report.ProgressPercentage);
            });

            // 7. Assert Output Video exists and is valid MP4
            Assert.True(File.Exists(testOutput));
            var outInfo = await probeService.ProbeFileAsync(testOutput);

            Assert.True(outInfo.HasVideo);
            Assert.True(outInfo.HasAudio);
            Assert.Equal(1080, outInfo.Width);
            Assert.Equal(1920, outInfo.Height);
            Assert.True(Math.Abs(outInfo.DurationSeconds - voiceAnalysis.ProcessedDurationSeconds) < 0.6);
            Assert.True(reportedProgress.Count > 0);
        }
        finally
        {
            try
            {
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup lock
            }
        }
    }
}
