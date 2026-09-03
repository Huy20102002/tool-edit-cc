using System.Text;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.Hardware;

public class HardwareDetector : IHardwareDetector
{
    private readonly IFFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly ILogger<HardwareDetector>? _logger;
    private HardwareCapabilities? _cachedCapabilities;

    public HardwareDetector(
        IFFmpegLocator locator,
        IFFmpegProcessRunner runner,
        ILogger<HardwareDetector>? logger = null)
    {
        _locator = locator;
        _runner = runner;
        _logger = logger;
    }

    public async Task<HardwareCapabilities> DetectCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCapabilities != null)
            return _cachedCapabilities;

        var caps = new HardwareCapabilities
        {
            LogicalCores = Environment.ProcessorCount
        };

        var ffmpegPath = _locator.GetFFmpegPath();

        // 1. Check -encoders string
        try
        {
            var encodersOutput = new StringBuilder();
            var exitCode = await _runner.ExecuteAsync(
                ffmpegPath,
                "-encoders",
                line => encodersOutput.AppendLine(line),
                null,
                cancellationToken).ConfigureAwait(false);

            if (exitCode == 0)
            {
                var outStr = encodersOutput.ToString();
                caps.HasNvidia = outStr.Contains("h264_nvenc") || outStr.Contains("nvenc");
                caps.HasAmd = outStr.Contains("h264_amf");
                caps.HasIntel = outStr.Contains("h264_qsv");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to query FFmpeg encoders list");
        }

        // 2. Perform live encode test for h264_nvenc (to verify Nvidia driver compatibility)
        if (caps.HasNvidia)
        {
            var nvencTestPassed = await TestEncoderAsync(ffmpegPath, "h264_nvenc", cancellationToken).ConfigureAwait(false);
            if (nvencTestPassed)
            {
                caps.SupportsNvencH264 = true;
                caps.NvencProbeStatus = "PASS (NVIDIA NVENC Native SDK)";
                caps.GpuName = "NVIDIA GeForce / RTX (Tăng tốc NVENC Native)";
                caps.GpuAccelerationName = "NVIDIA NVENC (h264_nvenc)";
            }
            else
            {
                _logger?.LogInformation("h264_nvenc test failed (driver version mismatch), testing h264_mf (MediaFoundation GPU)...");
                // Test Windows MediaFoundation GPU Hardware Acceleration for NVIDIA
                var mfTestPassed = await TestEncoderAsync(ffmpegPath, "h264_mf", cancellationToken).ConfigureAwait(false);
                if (mfTestPassed)
                {
                    caps.SupportsMediaFoundationH264 = true;
                    caps.NvencProbeStatus = "PASS (NVIDIA GPU qua DirectX MediaFoundation MFT)";
                    caps.GpuName = "NVIDIA GeForce / RTX (Tăng tốc GPU MediaFoundation)";
                    caps.GpuAccelerationName = "NVIDIA GPU Hardware (h264_mf)";
                }
                else
                {
                    caps.NvencProbeStatus = "FAIL (Yêu cầu driver NVIDIA 570+ hoặc MediaFoundation)";
                    caps.GpuName = "CPU Đa luồng (libx264)";
                    caps.GpuAccelerationName = "CPU (libx264)";
                }
            }
        }
        else if (caps.HasIntel)
        {
            var qsvTestPassed = await TestEncoderAsync(ffmpegPath, "h264_qsv", cancellationToken).ConfigureAwait(false);
            if (qsvTestPassed)
            {
                caps.SupportsQsvH264 = true;
                caps.GpuName = "Intel HD / Iris / Arc (Tăng tốc QSV)";
                caps.GpuAccelerationName = "Intel QuickSync (h264_qsv)";
            }
        }
        else if (caps.HasAmd)
        {
            var amfTestPassed = await TestEncoderAsync(ffmpegPath, "h264_amf", cancellationToken).ConfigureAwait(false);
            if (amfTestPassed)
            {
                caps.SupportsAmfH264 = true;
                caps.GpuName = "AMD Radeon (Tăng tốc AMF)";
                caps.GpuAccelerationName = "AMD Radeon (h264_amf)";
            }
        }

        if (!caps.SupportsNvencH264 && !caps.SupportsMediaFoundationH264 && !caps.SupportsQsvH264 && !caps.SupportsAmfH264)
        {
            caps.GpuName = "CPU Đa luồng (libx264)";
            caps.GpuAccelerationName = "CPU (libx264)";
        }

        _cachedCapabilities = caps;
        return caps;
    }

    private async Task<bool> TestEncoderAsync(string ffmpegPath, string encoderName, CancellationToken ct)
    {
        try
        {
            var testArgs = $"-f lavfi -i color=c=black:s=64x64:d=0.1 -c:v {encoderName} -f null -";
            var exitCode = await _runner.ExecuteAsync(ffmpegPath, testArgs, null, null, ct).ConfigureAwait(false);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
