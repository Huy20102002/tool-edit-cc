using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.AudioEngine;

public class AudioAnalyzer : IAudioAnalyzer
{
    private readonly ISilenceDetector _silenceDetector;
    private readonly IWaveformGenerator _waveformGenerator;
    private readonly ILogger<AudioAnalyzer>? _logger;

    public AudioAnalyzer(
        ISilenceDetector silenceDetector,
        IWaveformGenerator waveformGenerator,
        ILogger<AudioAnalyzer>? logger = null)
    {
        _silenceDetector = silenceDetector;
        _waveformGenerator = waveformGenerator;
        _logger = logger;
    }

    public async Task<AudioAnalysisResult> AnalyzeVoiceAsync(
        string audioFilePath,
        double silenceThresholdDb,
        int minSilenceDurationMs,
        int paddingBeforeMs,
        int paddingAfterMs,
        int waveformSamples = 400,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Analyzing voice file: {Path}", audioFilePath);

        var silenceTask = _silenceDetector.AnalyzeSilenceAsync(
            audioFilePath,
            silenceThresholdDb,
            minSilenceDurationMs,
            paddingBeforeMs,
            paddingAfterMs,
            cancellationToken);

        var waveformTask = _waveformGenerator.GenerateWaveformAsync(
            audioFilePath,
            waveformSamples,
            cancellationToken);

        await Task.WhenAll(silenceTask, waveformTask).ConfigureAwait(false);

        var result = await silenceTask.ConfigureAwait(false);
        result.WaveformPoints = await waveformTask.ConfigureAwait(false);

        _logger?.LogInformation(
            "Voice analysis finished. Original: {Orig:F2}s, Processed: {Proc:F2}s, Cut: {Cut:F2}s ({Pct:F1}%)",
            result.OriginalDurationSeconds,
            result.ProcessedDurationSeconds,
            result.SilenceDurationRemovedSeconds,
            result.SilenceRemovalPercentage);

        return result;
    }
}
