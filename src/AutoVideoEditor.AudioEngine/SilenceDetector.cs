using System.Globalization;
using System.Text.RegularExpressions;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.AudioEngine;

public class SilenceDetector : ISilenceDetector
{
    private readonly IFFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly IFFprobeService _probeService;
    private readonly ILogger<SilenceDetector>? _logger;

    private static readonly Regex SilenceStartRegex = new(@"silence_start:\s*([0-9\.]+)", RegexOptions.Compiled);
    private static readonly Regex SilenceEndRegex = new(@"silence_end:\s*([0-9\.]+)\s*\|\s*silence_duration:\s*([0-9\.]+)", RegexOptions.Compiled);

    public SilenceDetector(
        IFFmpegLocator locator,
        IFFmpegProcessRunner runner,
        IFFprobeService probeService,
        ILogger<SilenceDetector>? logger = null)
    {
        _locator = locator;
        _runner = runner;
        _probeService = probeService;
        _logger = logger;
    }

    public async Task<AudioAnalysisResult> AnalyzeSilenceAsync(
        string audioFilePath,
        double silenceThresholdDb,
        int minSilenceDurationMs,
        int paddingBeforeMs,
        int paddingAfterMs,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException($"Voice audio file not found: {audioFilePath}", audioFilePath);
        }

        // 1. Probe audio for exact total duration and sample rate
        var mediaInfo = await _probeService.ProbeFileAsync(audioFilePath, cancellationToken).ConfigureAwait(false);
        var originalDuration = mediaInfo.DurationSeconds;

        if (originalDuration <= 0)
        {
            throw new InvalidOperationException($"Invalid or zero audio duration for '{Path.GetFileName(audioFilePath)}'");
        }

        // 2. Run FFmpeg silencedetect
        var minSilenceSec = Math.Max(0.1, minSilenceDurationMs / 1000.0);
        var ffmpegPath = _locator.GetFFmpegPath();
        var arguments = $"-i \"{audioFilePath}\" -af \"silencedetect=noise={silenceThresholdDb.ToString("F1", CultureInfo.InvariantCulture)}dB:d={minSilenceSec.ToString("F3", CultureInfo.InvariantCulture)}\" -f null -";

        var detectedSilences = new List<(double Start, double End)>();
        double? currentSilenceStart = null;

        var exitCode = await _runner.ExecuteAsync(
            ffmpegPath,
            arguments,
            null,
            line =>
            {
                if (line == null) return;

                var startMatch = SilenceStartRegex.Match(line);
                if (startMatch.Success && double.TryParse(startMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var startVal))
                {
                    currentSilenceStart = startVal;
                }

                var endMatch = SilenceEndRegex.Match(line);
                if (endMatch.Success && double.TryParse(endMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var endVal))
                {
                    var sStart = currentSilenceStart ?? Math.Max(0, endVal - (double.TryParse(endMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0));
                    detectedSilences.Add((sStart, endVal));
                    currentSilenceStart = null;
                }
            },
            cancellationToken).ConfigureAwait(false);

        // If a silence started near the end of audio and never triggered silence_end
        if (currentSilenceStart.HasValue && currentSilenceStart.Value < originalDuration)
        {
            detectedSilences.Add((currentSilenceStart.Value, originalDuration));
        }

        // 3. Convert silence intervals into speech intervals with padding
        var paddingBeforeSec = paddingBeforeMs / 1000.0;
        var paddingAfterSec = paddingAfterMs / 1000.0;

        var rawSpeechIntervals = CalculateRawSpeechIntervals(detectedSilences, originalDuration);
        var paddedSpeechSegments = ApplyPaddingAndMerge(rawSpeechIntervals, originalDuration, paddingBeforeSec, paddingAfterSec);

        // Create SilenceSegment list for UI display
        var silenceSegments = new List<SilenceSegment>();
        for (int i = 0; i < detectedSilences.Count; i++)
        {
            var s = detectedSilences[i];
            silenceSegments.Add(new SilenceSegment(i + 1, s.Start, s.End));
        }

        var processedDuration = paddedSpeechSegments.Sum(s => s.DurationSeconds);

        // Fallback: If no speech was detected (e.g. threshold too aggressive), retain entire file
        if (paddedSpeechSegments.Count == 0)
        {
            paddedSpeechSegments.Add(new SpeechSegment(1, 0, originalDuration));
            processedDuration = originalDuration;
        }

        return new AudioAnalysisResult
        {
            FilePath = audioFilePath,
            OriginalDurationSeconds = originalDuration,
            ProcessedDurationSeconds = processedDuration,
            SpeechSegments = paddedSpeechSegments,
            SilenceSegments = silenceSegments,
            SampleRate = mediaInfo.AudioSampleRate > 0 ? mediaInfo.AudioSampleRate : 44100,
            Channels = mediaInfo.AudioChannels > 0 ? mediaInfo.AudioChannels : 2,
            SilenceThresholdDb = silenceThresholdDb,
            MinSilenceDurationMs = minSilenceDurationMs,
            PaddingBeforeMs = paddingBeforeMs,
            PaddingAfterMs = paddingAfterMs
        };
    }

    public static List<(double Start, double End)> CalculateRawSpeechIntervals(
        List<(double Start, double End)> silences,
        double totalDuration)
    {
        var speech = new List<(double Start, double End)>();
        double currentPos = 0.0;

        // Sort silences chronologically
        var sortedSilences = silences.OrderBy(s => s.Start).ToList();

        foreach (var (silenceStart, silenceEnd) in sortedSilences)
        {
            var clampedStart = Math.Max(0.0, Math.Min(totalDuration, silenceStart));
            var clampedEnd = Math.Max(0.0, Math.Min(totalDuration, silenceEnd));

            if (clampedStart > currentPos)
            {
                speech.Add((currentPos, clampedStart));
            }
            currentPos = Math.Max(currentPos, clampedEnd);
        }

        if (currentPos < totalDuration)
        {
            speech.Add((currentPos, totalDuration));
        }

        return speech;
    }

    public static List<SpeechSegment> ApplyPaddingAndMerge(
        List<(double Start, double End)> rawSpeech,
        double totalDuration,
        double paddingBeforeSec,
        double paddingAfterSec)
    {
        if (rawSpeech.Count == 0)
            return new List<SpeechSegment>();

        var expanded = new List<(double Start, double End)>();
        foreach (var (start, end) in rawSpeech)
        {
            var pStart = Math.Max(0.0, start - paddingBeforeSec);
            var pEnd = Math.Min(totalDuration, end + paddingAfterSec);
            if (pEnd > pStart)
            {
                expanded.Add((pStart, pEnd));
            }
        }

        if (expanded.Count == 0)
            return new List<SpeechSegment>();

        // Merge overlapping or touching intervals
        var merged = new List<(double Start, double End)>();
        var current = expanded[0];

        for (int i = 1; i < expanded.Count; i++)
        {
            var next = expanded[i];
            if (next.Start <= current.End)
            {
                // Overlap: expand current end
                current = (current.Start, Math.Max(current.End, next.End));
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        // Convert to SpeechSegment models (filter tiny noise intervals < 0.03s)
        var result = new List<SpeechSegment>();
        int idx = 1;
        foreach (var m in merged)
        {
            if (m.End - m.Start >= 0.03)
            {
                result.Add(new SpeechSegment(idx++, m.Start, m.End));
            }
        }

        return result;
    }
}
