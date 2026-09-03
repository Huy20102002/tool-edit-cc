using System.Globalization;
using System.Text.RegularExpressions;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.VideoEngine;

public class VideoRenderer : IVideoRenderer
{
    private readonly IFFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly ILogService? _logService;
    private readonly ILogger<VideoRenderer>? _logger;

    private static readonly Regex ProgressRegex = new(
        @"frame=\s*(\d+)\s+fps=\s*([0-9\.]+)\s+q=.*time=\s*([0-9:.]+)\s+bitrate=.*speed=\s*([0-9\.]+)x",
        RegexOptions.Compiled);

    public VideoRenderer(
        IFFmpegLocator locator,
        IFFmpegProcessRunner runner,
        IHardwareDetector hardwareDetector,
        ILogService? logService = null,
        ILogger<VideoRenderer>? logger = null)
    {
        _locator = locator;
        _runner = runner;
        _hardwareDetector = hardwareDetector;
        _logService = logService;
        _logger = logger;
    }

    public async Task RenderAsync(
        VideoJob job,
        TimelinePlan timelinePlan,
        Action<JobProgressReport> onProgress,
        CancellationToken cancellationToken = default)
    {
        var ffmpegPath = _locator.GetFFmpegPath();
        var hardwareCaps = await _hardwareDetector.DetectCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        var chosenEncoder = hardwareCaps.GetEncoderName(job.Preset.HardwareEncoder, job.Preset.VideoCodec);
        var tag = $"[JOB {job.OrderIndex:D3}]";

        job.EncoderUsed = chosenEncoder;
        _logger?.LogInformation("Starting render for Job {JobId} using encoder {Encoder}", job.Id, chosenEncoder);

        _logService?.LogInfo($"Phần cứng: {hardwareCaps.GpuName}", job.Id.ToString(), tag);
        _logService?.LogInfo($"Kiểm tra GPU: {hardwareCaps.NvencProbeStatus}", job.Id.ToString(), tag);
        _logService?.LogInfo($"Bộ mã hóa: {chosenEncoder} | Khung hình: {job.Preset.ResolutionWidth}x{job.Preset.ResolutionHeight} @ {job.Preset.Fps} FPS | Pixel format: yuv420p", job.Id.ToString(), tag);

        var success = await TryRenderInternalAsync(
            ffmpegPath,
            chosenEncoder,
            job,
            timelinePlan,
            onProgress,
            tag,
            cancellationToken).ConfigureAwait(false);

        if (!success && chosenEncoder != "libx264" && chosenEncoder != "libx265")
        {
            // Fallback to CPU libx264
            var fallbackEncoder = job.Preset.VideoCodec == VideoCodecType.H264 ? "libx264" : "libx265";
            _logger?.LogWarning("Hardware encoder {Encoder} failed for Job {JobId}. Falling back to CPU encoder {Fallback}...", chosenEncoder, job.Id, fallbackEncoder);
            _logService?.LogWarning($"[CẢNH BÁO] Bộ mã hóa {chosenEncoder} không thể xuất file. Chuyển sang CPU encoder ({fallbackEncoder})...", job.Id.ToString(), tag);
            
            job.EncoderUsed = $"{fallbackEncoder} (CPU Fallback)";

            onProgress(new JobProgressReport
            {
                JobId = job.Id,
                Status = JobStatus.Rendering,
                StepDescription = "Chuyển sang bộ mã hóa CPU (Fallback)...",
                ProgressPercentage = 0
            });

            var fallbackSuccess = await TryRenderInternalAsync(
                ffmpegPath,
                fallbackEncoder,
                job,
                timelinePlan,
                onProgress,
                tag,
                cancellationToken).ConfigureAwait(false);

            if (!fallbackSuccess)
            {
                _logService?.LogError($"[LỖI] Xuất video thất bại với cả GPU và CPU.", null, job.Id.ToString(), tag);
                throw new InvalidOperationException($"Xuất video thất bại với cả GPU và CPU encoder. Vui lòng kiểm tra file đầu vào hoặc dung lượng ổ đĩa.");
            }
        }
        else if (!success)
        {
            _logService?.LogError($"[LỖI] Xuất video thất bại với bộ mã hóa {chosenEncoder}.", null, job.Id.ToString(), tag);
            throw new InvalidOperationException($"Xuất video thất bại. Vui lòng kiểm tra file đầu vào hoặc dung lượng ổ đĩa.");
        }
    }

    private async Task<bool> TryRenderInternalAsync(
        string ffmpegPath,
        string encoderName,
        VideoJob job,
        TimelinePlan timelinePlan,
        Action<JobProgressReport> onProgress,
        string tag,
        CancellationToken cancellationToken)
    {
        var (arguments, _) = FilterGraphBuilder.BuildRenderCommand(
            job,
            timelinePlan,
            job.Preset,
            encoderName,
            job.OutputPath);

        // LOG FULL FFMPEG COMMAND FOR TRANSPARENCY & EASY DEBUGGING
        _logService?.LogInfo($"FFmpeg Command: {ffmpegPath} {arguments}", job.Id.ToString(), tag);

        var targetDuration = timelinePlan.TargetMasterDurationSeconds;
        var totalExpectedFrames = (long)(targetDuration * job.Preset.Fps);
        var lastReportTime = DateTime.UtcNow;
        var lastStderrLines = new List<string>();

        var exitCode = await _runner.ExecuteAsync(
            ffmpegPath,
            arguments,
            null,
            line =>
            {
                if (line == null) return;

                lock (lastStderrLines)
                {
                    if (lastStderrLines.Count > 30) lastStderrLines.RemoveAt(0);
                    lastStderrLines.Add(line);
                }

                var match = ProgressRegex.Match(line);
                if (match.Success)
                {
                    long.TryParse(match.Groups[1].Value, out var frame);
                    double.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var fps);
                    var timeStr = match.Groups[3].Value;
                    double.TryParse(match.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var speed);

                    var parsedTime = ParseTimeSpan(timeStr);
                    var percent = targetDuration > 0
                        ? Math.Clamp((parsedTime.TotalSeconds / targetDuration) * 100.0, 0.0, 99.9)
                        : 0.0;

                    TimeSpan? eta = null;
                    if (speed > 0.05 && targetDuration > parsedTime.TotalSeconds)
                    {
                        var remSec = (targetDuration - parsedTime.TotalSeconds) / speed;
                        if (remSec > 0 && remSec < 86400)
                        {
                            eta = TimeSpan.FromSeconds(remSec);
                        }
                    }

                    if ((DateTime.UtcNow - lastReportTime).TotalMilliseconds > 100)
                    {
                        lastReportTime = DateTime.UtcNow;
                        onProgress(new JobProgressReport
                        {
                            JobId = job.Id,
                            Status = JobStatus.Rendering,
                            StepDescription = $"Đang mã hóa video... ({percent:F0}%)",
                            ProgressPercentage = percent,
                            CurrentFrame = frame,
                            TotalFrames = totalExpectedFrames,
                            CurrentFps = fps,
                            Speed = speed,
                            CurrentTime = parsedTime,
                            TotalTime = TimeSpan.FromSeconds(targetDuration),
                            EstimatedTimeRemaining = eta,
                            Details = $"FPS: {fps:F0} | Tốc độ: {speed:F1}x"
                        });
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0)
        {
            var meaningfulLines = lastStderrLines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("frame=")).TakeLast(6);
            var errSnippet = string.Join("\n  > ", meaningfulLines);
            _logService?.LogError($"FFmpeg Exit Code: {exitCode}\n  > {errSnippet}", null, job.Id.ToString(), tag);
        }

        return exitCode == 0 && File.Exists(job.OutputPath) && new FileInfo(job.OutputPath).Length > 1000;
    }

    private static TimeSpan ParseTimeSpan(string timeStr)
    {
        if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }

        var parts = timeStr.Split(':');
        if (parts.Length == 3 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var h) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var m) &&
            double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
        {
            return TimeSpan.FromSeconds(h * 3600 + m * 60 + s);
        }

        return TimeSpan.Zero;
    }
}
