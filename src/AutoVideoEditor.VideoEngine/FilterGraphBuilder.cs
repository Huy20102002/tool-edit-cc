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

        // 1. Hardware acceleration decoding flag if available
        bool isGpu = encoderName.Contains("nvenc") || encoderName.Contains("qsv") || encoderName.Contains("amf") || encoderName.Contains("mf");

        // 2. Input flags and files
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

        // 3. Build Video Filter Graph (CapCut Optimized Ultra-Fast Background Blur + Transitions + 4K to FullHD Auto Scale)
        string finalVideoLabel;

        var activeTransitions = timelinePlan.Transitions.Where(t => t.IsActiveTransition).ToList();

        if (activeTransitions.Count > 0 && timelinePlan.Scenes.Count > 1)
        {
            // Build scene-based transition graph with exact xfade offsets
            finalVideoLabel = BuildSceneTransitionsFilterGraph(
                sbFilter,
                videoInputs,
                job.VideoMetadatas,
                timelinePlan.Scenes,
                timelinePlan.Transitions,
                preset.CropMode,
                targetW,
                targetH,
                fps
            );
        }
        else if (videoInputs.Count == 1)
        {
            var rawDur = job.VideoMetadatas.Count > 0 ? job.VideoMetadatas[0].DurationSeconds : 0;
            var inW = job.VideoMetadatas.Count > 0 ? job.VideoMetadatas[0].Width : 0;
            var inH = job.VideoMetadatas.Count > 0 ? job.VideoMetadatas[0].Height : 0;

            var cropFilter = BuildAspectAndCropFilter("0:v", "v_proc", preset.CropMode, targetW, targetH, fps, trimStart, trimEnd, rawDur, inW, inH);
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
                var inW = job.VideoMetadatas.Count > i ? job.VideoMetadatas[i].Width : 0;
                var inH = job.VideoMetadatas.Count > i ? job.VideoMetadatas[i].Height : 0;

                var filter = BuildAspectAndCropFilter($"{i}:v", label, preset.CropMode, targetW, targetH, fps, trimStart, trimEnd, rawDur, inW, inH);
                sbFilter.Append(filter);
                sbFilter.Append("; ");
                concatLabels.Add($"[{label}]");
            }

            var concatString = string.Join("", concatLabels);
            sbFilter.Append($"{concatString}concat=n={videoInputs.Count}:v=1:a=0[v_concat]");
            finalVideoLabel = "[v_concat]";
        }

        if (!sbFilter.ToString().TrimEnd().EndsWith(";"))
        {
            sbFilter.Append("; ");
        }
        else
        {
            sbFilter.Append(" ");
        }

        // 4. Build Audio Speech Filter Graph (Silence Removal & Fast High-Quality Normalization)
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
                sbFilter.Append($"; [a_cut]loudnorm=I={lufs}:LRA=11:TP=-1.5:linear=true,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
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
                sbFilter.Append($"; [a_cut]loudnorm=I={lufs}:LRA=11:TP=-1.5:linear=true,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
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
                sbFilter.Append($"[{voiceInputIndex}:a]loudnorm=I={lufs}:LRA=11:TP=-1.5:linear=true,aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_norm]");
                finalAudioLabel = "[a_norm]";
            }
            else
            {
                sbFilter.Append($"[{voiceInputIndex}:a]aformat=sample_fmts=fltp:sample_rates={preset.AudioSampleRate}:channel_layouts=stereo[a_out]");
                finalAudioLabel = "[a_out]";
            }
        }

        // 5. Video Codec and Encoder Settings (CapCut Ultra-Lightweight Tuning)
        var cleanFilter = sbFilter.ToString().TrimEnd(' ', ';');
        sbArgs.Append($"-filter_complex \"{cleanFilter}\" ");
        sbArgs.Append($"-map \"{finalVideoLabel}\" -map \"{finalAudioLabel}\" ");

        // Truncate precisely at target duration
        sbArgs.Append($"-t {targetDuration.ToString("F3", CultureInfo.InvariantCulture)} ");

        // Video encoder settings
        sbArgs.Append($"-c:v {encoderName} ");

        // Standard GOP size for smooth playback
        sbArgs.Append($"-g {gopSize} ");

        // CapCut-Optimized Hardware & CPU Profiles
        if (encoderName.Contains("nvenc"))
        {
            var bitrate = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 15000;
            sbArgs.Append($"-preset p4 -tune ll -b:v {bitrate}k -maxrate:v {bitrate * 1.3:F0}k -bufsize:v {bitrate * 2}k -pix_fmt yuv420p ");
        }
        else if (encoderName.Contains("mf"))
        {
            var br = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 15000;
            sbArgs.Append($"-b:v {br}k -pix_fmt yuv420p ");
        }
        else if (encoderName.Contains("qsv"))
        {
            var br = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 15000;
            sbArgs.Append($"-preset medium -b:v {br}k -global_quality 20 -pix_fmt nv12 ");
        }
        else if (encoderName.Contains("amf"))
        {
            var br = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 15000;
            sbArgs.Append($"-quality speed -rc cbr -b:v {br}k -pix_fmt yuv420p ");
        }
        else
        {
            // libx264 (CPU - Fast & lightweight, restricted threads to prevent freezing)
            var bitrate = preset.CustomVideoBitrateKbps > 0 ? preset.CustomVideoBitrateKbps : 15000;
            sbArgs.Append($"-preset superfast -b:v {bitrate}k -threads 3 -pix_fmt yuv420p ");
        }

        // Audio encoder settings (AAC 192k stereo)
        var aCodec = preset.AudioCodec == AudioCodecType.AAC ? "aac" : "libmp3lame";
        sbArgs.Append($"-c:a {aCodec} -b:a {preset.AudioBitrateKbps}k -ar {preset.AudioSampleRate} ");

        // Output flags: web streaming optimization (faststart) and overwrite
        sbArgs.Append($"-movflags +faststart -y \"{outputPath}\"");

        return (sbArgs.ToString(), cleanFilter);
    }

    private static string BuildSceneTransitionsFilterGraph(
        StringBuilder sbFilter,
        IReadOnlyList<string> videoInputs,
        IReadOnlyList<MediaFileInfo> videoMetadatas,
        IReadOnlyList<SceneSegment> scenes,
        IReadOnlyList<TransitionPlanItem> transitions,
        CropMode cropMode,
        int targetW,
        int targetH,
        int fps)
    {
        // 1. Split single video input into N branches if needed
        if (videoInputs.Count == 1)
        {
            var rawSplitLabels = new List<string>();
            for (int i = 0; i < scenes.Count; i++)
            {
                rawSplitLabels.Add($"[v_in_{i}]");
            }
            sbFilter.Append($"[0:v]split={scenes.Count}{string.Join("", rawSplitLabels)}; ");
        }

        // 2. Process each scene into a scaled/formatted intermediate stream
        var sceneLabels = new List<string>();
        for (int i = 0; i < scenes.Count; i++)
        {
            var sc = scenes[i];
            var scLabel = $"sc_{i}";
            var inputLabel = videoInputs.Count == 1 ? $"v_in_{i}" : $"{Math.Min(i, videoInputs.Count - 1)}:v";
            var inW = (videoMetadatas.Count > 0 && i < videoMetadatas.Count) ? videoMetadatas[i].Width : (videoMetadatas.Count > 0 ? videoMetadatas[0].Width : 0);
            var inH = (videoMetadatas.Count > 0 && i < videoMetadatas.Count) ? videoMetadatas[i].Height : (videoMetadatas.Count > 0 ? videoMetadatas[0].Height : 0);

            var sStart = sc.StartSeconds.ToString("F3", CultureInfo.InvariantCulture);
            var sEnd = sc.EndSeconds.ToString("F3", CultureInfo.InvariantCulture);

            var trimFilter = $"trim=start={sStart}:end={sEnd},setpts=PTS-STARTPTS,";
            var cropFilter = BuildAspectAndCropFilterCore(inputLabel, scLabel, cropMode, targetW, targetH, fps, trimFilter, inW, inH);
            sbFilter.Append(cropFilter);
            sbFilter.Append("; ");
            sceneLabels.Add($"[{scLabel}]");
        }

        // 3. Chain scenes together using xfade for active transitions or concat for cuts
        string currentStream = sceneLabels[0];
        double accumulatedDuration = scenes[0].DurationSeconds;

        for (int i = 0; i < transitions.Count && (i + 1) < scenes.Count; i++)
        {
            var trans = transitions[i];
            var nextScene = scenes[i + 1];
            var nextLabel = sceneLabels[i + 1];
            var outLabel = $"v_chain_{i}";

            if (trans.IsActiveTransition)
            {
                var xfadeType = MapToFFmpegXfade(trans.TransitionType);
                var durStr = trans.DurationSeconds.ToString("F2", CultureInfo.InvariantCulture);
                var offset = Math.Max(0.01, accumulatedDuration - trans.DurationSeconds);
                var offsetStr = offset.ToString("F3", CultureInfo.InvariantCulture);

                sbFilter.Append($"{currentStream}{nextLabel}xfade=transition={xfadeType}:duration={durStr}:offset={offsetStr}[{outLabel}]; ");
                currentStream = $"[{outLabel}]";
                accumulatedDuration = offset + nextScene.DurationSeconds;
            }
            else
            {
                // Pure CUT: concat with next scene
                sbFilter.Append($"{currentStream}{nextLabel}concat=n=2:v=1:a=0[{outLabel}]; ");
                currentStream = $"[{outLabel}]";
                accumulatedDuration += nextScene.DurationSeconds;
            }
        }

        return currentStream;
    }

    private static string MapToFFmpegXfade(TransitionType type)
    {
        return type switch
        {
            TransitionType.Fade => "fade",
            TransitionType.Dissolve => "dissolve",
            TransitionType.Zoom => "circlecrop",
            TransitionType.Slide => "slideleft",
            TransitionType.Wipe => "wipeleft",
            _ => "dissolve"
        };
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
        double rawDurationSec = 0,
        int inWidth = 0,
        int inHeight = 0)
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

        return BuildAspectAndCropFilterCore(inputLabel, outputLabel, cropMode, targetW, targetH, fps, trimPrefix, inWidth, inHeight);
    }

    private static string BuildAspectAndCropFilterCore(
        string inputLabel,
        string outputLabel,
        CropMode cropMode,
        int targetW,
        int targetH,
        int fps,
        string trimPrefix,
        int inWidth = 0,
        int inHeight = 0)
    {
        // 1. Check if input is already matching target aspect ratio (e.g. vertical 9:16)
        bool aspectAlreadyMatches = false;
        if (inWidth > 0 && inHeight > 0 && targetW > 0 && targetH > 0)
        {
            double inRatio = (double)inWidth / inHeight;
            double targetRatio = (double)targetW / targetH;
            if (Math.Abs(inRatio - targetRatio) < 0.05)
            {
                aspectAlreadyMatches = true;
            }
        }

        // If aspect ratio already matches, directly scale/crop to target resolution (Fast path: avoids blur/overlay overhead)
        if (aspectAlreadyMatches && cropMode == CropMode.FitWithBlur)
        {
            return $"[{inputLabel}]{trimPrefix}scale={targetW}:{targetH}:force_original_aspect_ratio=increase,crop={targetW}:{targetH},fps={fps},settb=AVTB[{outputLabel}]";
        }

        switch (cropMode)
        {
            case CropMode.FitWithBlur:
                // CapCut-style Ultra Fast Blur:
                // Downscale background to tiny 135x240 thumbnail -> light boxblur -> bilinear upscale to target (Costs near 0% CPU!)
                return $"[{inputLabel}]{trimPrefix}split=2[bg_src_{outputLabel}][fg_src_{outputLabel}]; " +
                       $"[bg_src_{outputLabel}]scale=135:240:force_original_aspect_ratio=increase,crop=135:240,boxblur=3:1,scale={targetW}:{targetH}:flags=bilinear[bg_{outputLabel}]; " +
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
