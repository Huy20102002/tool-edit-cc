using System.Globalization;
using System.Text;
using System.Text.Json;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.FFmpeg;

public class FFprobeService : IFFprobeService
{
    private readonly IFFmpegLocator _locator;
    private readonly IFFmpegProcessRunner _runner;
    private readonly ILogger<FFprobeService>? _logger;

    public FFprobeService(
        IFFmpegLocator locator,
        IFFmpegProcessRunner runner,
        ILogger<FFprobeService>? logger = null)
    {
        _locator = locator;
        _runner = runner;
        _logger = logger;
    }

    public async Task<MediaFileInfo> ProbeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Media file does not exist: {filePath}", filePath);
        }

        var ffprobePath = _locator.GetFFprobePath();
        var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        var exitCode = await _runner.ExecuteAsync(
            ffprobePath,
            arguments,
            line => sbOut.AppendLine(line),
            line => sbErr.AppendLine(line),
            cancellationToken).ConfigureAwait(false);

        if (exitCode != 0 || sbOut.Length == 0)
        {
            var err = sbErr.ToString();
            _logger?.LogError("FFprobe failed with code {Code}: {Err}", exitCode, err);
            throw new InvalidOperationException($"FFprobe failed to inspect '{Path.GetFileName(filePath)}': {err}");
        }

        var json = sbOut.ToString();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var info = new MediaFileInfo
        {
            FilePath = filePath,
            FileSizeBytes = new FileInfo(filePath).Length
        };

        // Parse format
        if (root.TryGetProperty("format", out var formatElem))
        {
            if (formatElem.TryGetProperty("duration", out var durElem) &&
                double.TryParse(durElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            {
                info.DurationSeconds = duration;
            }

            if (formatElem.TryGetProperty("bit_rate", out var brElem) &&
                long.TryParse(brElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var bitrate))
            {
                info.BitrateBps = bitrate;
            }
        }

        // Parse streams
        if (root.TryGetProperty("streams", out var streamsElem) && streamsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streamsElem.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;

                if (codecType == "video" && !info.HasVideo)
                {
                    info.HasVideo = true;
                    info.VideoCodec = stream.TryGetProperty("codec_name", out var cn) ? (cn.GetString() ?? "") : "";

                    if (stream.TryGetProperty("width", out var wElem))
                        info.Width = wElem.GetInt32();

                    if (stream.TryGetProperty("height", out var hElem))
                        info.Height = hElem.GetInt32();

                    // Parse FPS
                    if (stream.TryGetProperty("r_frame_rate", out var rfrElem))
                    {
                        info.Fps = ParseFps(rfrElem.GetString());
                    }
                    else if (stream.TryGetProperty("avg_frame_rate", out var afrElem))
                    {
                        info.Fps = ParseFps(afrElem.GetString());
                    }

                    // Fallback duration if format duration was 0
                    if (info.DurationSeconds <= 0 && stream.TryGetProperty("duration", out var vDurElem) &&
                        double.TryParse(vDurElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var vDur))
                    {
                        info.DurationSeconds = vDur;
                    }
                }
                else if (codecType == "audio" && !info.HasAudio)
                {
                    info.HasAudio = true;
                    info.AudioCodec = stream.TryGetProperty("codec_name", out var acn) ? (acn.GetString() ?? "") : "";

                    if (stream.TryGetProperty("channels", out var chElem))
                        info.AudioChannels = chElem.GetInt32();

                    if (stream.TryGetProperty("sample_rate", out var srElem) &&
                        int.TryParse(srElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sr))
                    {
                        info.AudioSampleRate = sr;
                    }

                    // Fallback duration if still 0
                    if (info.DurationSeconds <= 0 && stream.TryGetProperty("duration", out var aDurElem) &&
                        double.TryParse(aDurElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var aDur))
                    {
                        info.DurationSeconds = aDur;
                    }
                }
            }
        }

        return info;
    }

    private static double ParseFps(string? fpsString)
    {
        if (string.IsNullOrWhiteSpace(fpsString)) return 30.0;
        var parts = fpsString.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) && den > 0)
        {
            return num / den;
        }

        if (double.TryParse(fpsString, NumberStyles.Any, CultureInfo.InvariantCulture, out var direct))
        {
            return direct;
        }

        return 30.0;
    }
}
