using System.Globalization;
using System.Text;
using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.VideoEngine;

public class FilterGraphBuilder
{
    public static (string Arguments, string? ComplexFilter) BuildRenderCommand(
        VideoJob job,
        TimelinePlan timelinePlan,
        ExportPreset preset,
        string encoderName,
        string outputPath)
    {
        var sbArgs = new StringBuilder();
        var sbFilter = new StringBuilder();

        var videoInputs = job.VideoPaths;
        var voicePath = job.VoicePath;
        var targetDuration = timelinePlan.TargetMasterDurationSeconds;
        var targetW = preset.ResolutionWidth;
        var targetH = preset.ResolutionHeight;
        var fps = preset.Fps;
        var gopSize = Math.Max(30, (int)(fps * 2));

        var trimStart = preset.VideoTrimStartSeconds;
        var trimEnd = preset.VideoTrimEndSeconds;

        // 1. Input flags and files
        if (videoInputs.Count == 1 && timelinePlan.RequiresVideoLooping)
        {
            var loopCount = Math.Max(1, timelinePlan.TotalVideoLoops + 2);
            sbArgs.Append($"-stream_loop {loopCount} ");
        }

        // Add Video Inputs
        for (int i = 0; i < videoInputs.Count; i++)
        {
            sbArgs.Append($"-i \"{videoInputs[i]}\" ");
        }

        // Add Voice Audio Input
        var voiceInputIndex = videoInputs.Count;
        sbArgs.Append($"-i \"{voicePath}\" ");

        // 2. Build Video Filter Graph (CapCut Optimized Ultra-Fast Background Blur)
        string finalVideoLabel;

        if (videoInputs.Count == 1)
        {
            var rawDur = job.VideoMetadatas.Count > 0 ? job.VideoMetadatas[0].DurationSeconds : 0;
            var cropFilter = BuildAspectAndCropFilter("0:v", "v_proc", preset.CropMode, targetW, targetH, fps, trimStart, trimEnd, rawDur);
            sbFilter.Append(cropFilter);
            finalVideoLabel = "[v_proc]";
        }
        else
        {
            var concatLabels = new List<string>();
            for (int i = 0; i < videoInputs.Count; i++)
            {
                var label = $"v_scaled_{i}";
                var rawDur = job.VideoMetadatas.Count > i ? job.VideoMetadatas[i].DurationSeconds : 0;
                var filter = BuildAspectAndCropFilter($"{i}:v", label, preset.CropMode, targetW, targetH, fps, trimStart, trimEnd, rawDur);
                sbFilter.Append(filter);
                sbFilter.Append("; ");
                concatLabels.Add($"[{label}]");
            }

            var concatString = string.Join("", concatLabels);
            sbFilter.Append($"{concatString}concat=n={videoInputs.Count}:v=1:a=0[v_concat]");
            finalVideoLabel = "[v_concat]";
        }

        sbFilter.Append("; ");

        // 3. Build Audio Speech Filter Graph (Silence Removal & Normalization)
        var speechSegments = timelinePlan.AudioSpeechSegments;
        string finalAudioLabel;

        if (speechSegments.Count > 1)
        {
            var splitLabels = new List<string>();
            for (int i = 0; i < speechSegments.Count; i++)
            {
                splitLabels.Add($"[a_in_{i}]");
            }
            sbFilter.Append($"[{voiceInputIndex}:a]asplit={speechSegments.Count}{string.Join("", splitLabels)}; ");

            var audioLabels = new List<string>();
            for (int i = 0; i < speechSegments.Count; i++)
            {
                var seg = speechSegments[i];
                var sStart = seg.StartSeconds.ToString("F3", CultureInfo.InvariantCulture);
                var sEnd = seg.EndSeconds.ToString("F3", CultureInfo.InvariantCulture);
                var aLabel = $"a_seg_{i}";

                sbFilter.Append($"[a_in_{i}]atrim=start={sStart}:end={sEnd},asetpts=PTS-STARTPTS[{aLabel}]; ");
                audioLabels.Add($"[{aLabel}]");
            }

            var aConcatString = string.Join("", audioLabels);
            sbFilter.Append($"{aConcatString}concat=n={speechSegments.Count}:v=0:a=1[a_cut]");

            if (preset.NormalizeAudio)
            {
                var lufs = preset.TargetLufs.ToString("F1", CultureInfo.InvariantCulture);
                sbFilter.Append($"; [a_cut]loudnorm=I={lufs}:LRA=11:TP=-1.5,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
                finalAudioLabel = "[a_norm]";
            }
            else
            {
                sbFilter.Append($"; [a_cut]aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_out]");
                finalAudioLabel = "[a_out]";
            }
        }
        else if (speechSegments.Count == 1)
        {
            var seg = speechSegments[0];
            var sStart = seg.StartSeconds.ToString("F3", CultureInfo.InvariantCulture);
            var sEnd = seg.EndSeconds.ToString("F3", CultureInfo.InvariantCulture);

            sbFilter.Append($"[{voiceInputIndex}:a]atrim=start={sStart}:end={sEnd},asetpts=PTS-STARTPTS[a_cut]");

            if (preset.NormalizeAudio)
            {
                var lufs = preset.TargetLufs.ToString("F1", CultureInfo.InvariantCulture);
                sbFilter.Append($"; [a_cut]loudnorm=I={lufs}:LRA=11:TP=-1.5,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
                finalAudioLabel = "[a_norm]";
            }
            else
            {
                sbFilter.Append($"; [a_cut]aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_out]");
                finalAudioLabel = "[a_out]";
            }
        }
        else
        {
            if (preset.NormalizeAudio)
            {
                var lufs = preset.TargetLufs.ToString("F1", CultureInfo.InvariantCulture);
                sbFilter.Append($"[{voiceInputIndex}:a]loudnorm=I={lufs}:LRA=11:TP=-1.5,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
                finalAudioLabel = "[a_norm]";
            }
            else
            {
                sbFilter.Append($"[{voiceInputIndex}:a]aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_out]");
                finalAudioLabel = "[a_out]";
            }
        }

        // 4. Video Codec and Encoder Settings (Standard Universal Compatibility)
        sbArgs.Append($"-filter_complex \"{sbFilter}\" ");
        sbArgs.Append($"-map \"{finalVideoLabel}\" -map \"{finalAudioLabel}\" ");

        // Truncate precisely at target duration
        sbArgs.Append($"-t {targetDuration.ToString("F3", CultureInfo.InvariantCulture)} ");

        // Video encoder settings
        sbArgs.Append($"-c:v {encoderName} ");

        // Standard GOP size for smooth playback
        sbArgs.Append($"-g {gopSize} ");

        // Universal encoder rate-control settings
        if (encoderName.Contains("nvenc"))
        {
            sbArgs.Append("-preset medium -cq 21 -pix_fmt yuv420p ");
            if (preset.BitrateMode == VideoBitrateMode.Custom && preset.CustomVideoBitrateKbps > 0)
            {
                sbArgs.Append($"-b:v {preset.CustomVideoBitrateKbps}k -maxrate:v {preset.CustomVideoBitrateKbps * 1.5}k ");
            }
        }
        else if (encoderName.Contains("mf"))
        {
            // MediaFoundation GPU Acceleration (DirectX MFT for NVIDIA/Intel/AMD)
            var br = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 8000;
            sbArgs.Append($"-b:v {br}k -pix_fmt yuv420p ");
        }
        else if (encoderName.Contains("qsv"))
        {
            sbArgs.Append("-preset medium -global_quality 21 -pix_fmt nv12 ");
        }
        else if (encoderName.Contains("amf"))
        {
            sbArgs.Append("-quality speed -rc cqp -qp_i 21 -qp_p 21 -pix_fmt yuv420p ");
        }
        else
        {
            // libx264 / libx265 (CPU - Fast & Lightweight)
            sbArgs.Append("-preset veryfast -crf 21 -pix_fmt yuv420p ");
            if (preset.BitrateMode == VideoBitrateMode.Custom && preset.CustomVideoBitrateKbps > 0)
            {
                sbArgs.Append($"-b:v {preset.CustomVideoBitrateKbps}k ");
            }
        }

        // Audio encoder settings (AAC 192k stereo)
        var aCodec = preset.AudioCodec == AudioCodecType.AAC ? "aac" : "libmp3lame";
        sbArgs.Append($"-c:a {aCodec} -b:a {preset.AudioBitrateKbps}k -ar {preset.AudioSampleRate} ");

        // Output flags: web streaming optimization (faststart) and overwrite
        sbArgs.Append($"-movflags +faststart -y \"{outputPath}\"");

        return (sbArgs.ToString(), sbFilter.ToString());
    }

    private static string BuildAspectAndCropFilter(
        string inputLabel,
        string outputLabel,
        CropMode cropMode,
        int targetW,
        int targetH,
        int fps,
        double trimStartSec = 0,
        double trimEndSec = 0,
        double rawDurationSec = 0)
    {
        string trimPrefix = "";
        if (trimStartSec > 0.001 && trimEndSec > 0.001 && rawDurationSec > (trimStartSec + trimEndSec))
        {
            var endSec = rawDurationSec - trimEndSec;
            trimPrefix = $"trim=start={trimStartSec.ToString("F3", CultureInfo.InvariantCulture)}:end={endSec.ToString("F3", CultureInfo.InvariantCulture)},setpts=PTS-STARTPTS,";
        }
        else if (trimStartSec > 0.001)
        {
            trimPrefix = $"trim=start={trimStartSec.ToString("F3", CultureInfo.InvariantCulture)},setpts=PTS-STARTPTS,";
        }
        else if (trimEndSec > 0.001 && rawDurationSec > trimEndSec)
        {
            var endSec = rawDurationSec - trimEndSec;
            trimPrefix = $"trim=end={endSec.ToString("F3", CultureInfo.InvariantCulture)},setpts=PTS-STARTPTS,";
        }
        else
        {
            trimPrefix = "setpts=PTS-STARTPTS,";
        }

        switch (cropMode)
        {
            case CropMode.FitWithBlur:
                // CapCut-style Ultra Fast Blur: downscale to 270x480 -> light boxblur -> bilinear upscale to 1080x1920 (95% CPU reduction!)
                return $"[{inputLabel}]{trimPrefix}split=2[bg_src_{outputLabel}][fg_src_{outputLabel}]; " +
                       $"[bg_src_{outputLabel}]scale=270:480:force_original_aspect_ratio=increase,crop=270:480,boxblur=5:2,scale={targetW}:{targetH}:flags=bilinear[bg_{outputLabel}]; " +
                       $"[fg_src_{outputLabel}]scale={targetW}:{targetH}:force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2[fg_{outputLabel}]; " +
                       $"[bg_{outputLabel}][fg_{outputLabel}]overlay=(W-w)/2:(H-h)/2,fps={fps},settb=AVTB[{outputLabel}]";

            case CropMode.CenterCrop:
                return $"[{inputLabel}]{trimPrefix}scale={targetW}:{targetH}:force_original_aspect_ratio=increase,crop={targetW}:{targetH},fps={fps},settb=AVTB[{outputLabel}]";

            case CropMode.FitBlackBars:
                return $"[{inputLabel}]{trimPrefix}scale={targetW}:{targetH}:force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2,pad={targetW}:{targetH}:(ow-iw)/2:(oh-ih)/2:black,fps={fps},settb=AVTB[{outputLabel}]";

            case CropMode.Stretch:
            default:
                return $"[{inputLabel}]{trimPrefix}scale={targetW}:{targetH},fps={fps},settb=AVTB[{outputLabel}]";
        }
    }
}
