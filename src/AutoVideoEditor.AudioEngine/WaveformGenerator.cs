using System.Diagnostics;
using AutoVideoEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.AudioEngine;

public class WaveformGenerator : IWaveformGenerator
{
    private readonly IFFmpegLocator _locator;
    private readonly ILogger<WaveformGenerator>? _logger;

    public WaveformGenerator(
        IFFmpegLocator locator,
        ILogger<WaveformGenerator>? logger = null)
    {
        _locator = locator;
        _logger = logger;
    }

    public async Task<float[]> GenerateWaveformAsync(
        string audioFilePath,
        int sampleCount = 400,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath) || sampleCount <= 0)
        {
            return CreateFallbackWaveform(sampleCount);
        }

        try
        {
            var ffmpegPath = _locator.GetFFmpegPath();
            // Downsample audio to mono 8000Hz 16-bit PCM raw stream
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{audioFilePath}\" -vn -ac 1 -ar 8000 -f s16le -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return CreateFallbackWaveform(sampleCount);
            }

            using var memStream = new MemoryStream();
            var outCopyTask = process.StandardOutput.BaseStream.CopyToAsync(memStream, cancellationToken);
            var errDrainTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(outCopyTask, errDrainTask).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var pcmBytes = memStream.ToArray();
            var totalSamples = pcmBytes.Length / 2; // 16-bit = 2 bytes per sample

            if (totalSamples == 0)
            {
                return CreateFallbackWaveform(sampleCount);
            }

            var samplesPerBucket = Math.Max(1, totalSamples / sampleCount);
            var waveform = new float[sampleCount];

            for (int bucket = 0; bucket < sampleCount; bucket++)
            {
                var startSample = bucket * samplesPerBucket;
                var endSample = Math.Min(totalSamples, startSample + samplesPerBucket);
                float maxAbs = 0;

                for (int s = startSample; s < endSample; s++)
                {
                    var byteIndex = s * 2;
                    if (byteIndex + 1 < pcmBytes.Length)
                    {
                        short sample = (short)(pcmBytes[byteIndex] | (pcmBytes[byteIndex + 1] << 8));
                        var absVal = Math.Abs((float)sample / short.MaxValue);
                        if (absVal > maxAbs)
                        {
                            maxAbs = absVal;
                        }
                    }
                }

                waveform[bucket] = Math.Clamp((float)Math.Sqrt(maxAbs), 0.02f, 1.0f);
            }

            return waveform;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate waveform for {Path}", audioFilePath);
            return CreateFallbackWaveform(sampleCount);
        }
    }

    private static float[] CreateFallbackWaveform(int count)
    {
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = 0.2f;
        }
        return result;
    }
}
