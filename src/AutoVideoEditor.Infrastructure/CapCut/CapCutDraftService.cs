using System.Text.Json;
using System.Text.Json.Nodes;
using AutoVideoEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.CapCut;

public class CapCutDraftService : ICapCutDraftService
{
    private readonly IFFprobeService _probeService;
    private readonly ILogger<CapCutDraftService>? _logger;

    public CapCutDraftService(
        IFFprobeService probeService,
        ILogger<CapCutDraftService>? logger = null)
    {
        _probeService = probeService;
        _logger = logger;
    }

    public string? DetectCapCutDraftsRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "CapCut", "User Data", "Projects", "com.lveditor.draft"),
            Path.Combine(localAppData, "JianyingPro", "User Data", "Projects", "com.lveditor.draft")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        // Return standard CapCut path even if not yet created
        return candidates[0];
    }

    public List<CapCutProjectTemplateInfo> GetAvailableTemplates(string? customDraftsRootDir = null)
    {
        var draftsRoot = !string.IsNullOrWhiteSpace(customDraftsRootDir)
            ? customDraftsRootDir
            : DetectCapCutDraftsRootDirectory();

        var result = new List<CapCutProjectTemplateInfo>();
        if (string.IsNullOrWhiteSpace(draftsRoot) || !Directory.Exists(draftsRoot))
        {
            return result;
        }

        try
        {
            var dirs = Directory.GetDirectories(draftsRoot);
            foreach (var dir in dirs)
            {
                var draftContentFile = Path.Combine(dir, "draft_content.json");
                if (!File.Exists(draftContentFile))
                    continue;

                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var textCount = 0;
                    var stickerCount = 0;
                    var audioCount = 0;
                    var trackCount = 0;
                    var projName = dirInfo.Name;

                    // Read draft_meta_info.json if available
                    var metaFile = Path.Combine(dir, "draft_meta_info.json");
                    if (File.Exists(metaFile))
                    {
                        try
                        {
                            var metaJson = JsonNode.Parse(File.ReadAllText(metaFile))?.AsObject();
                            var metaName = metaJson?["draft_name"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(metaName))
                            {
                                projName = metaName;
                            }
                        }
                        catch
                        {
                            // fallback to dirInfo.Name
                        }
                    }

                    // Parse draft_content.json counts
                    try
                    {
                        var contentJson = JsonNode.Parse(File.ReadAllText(draftContentFile))?.AsObject();
                        if (contentJson != null)
                        {
                            if (contentJson.ContainsKey("materials") && contentJson["materials"] is JsonObject mat)
                            {
                                textCount = mat["texts"]?.AsArray().Count ?? 0;
                                stickerCount = mat["stickers"]?.AsArray().Count ?? 0;
                                audioCount = mat["audios"]?.AsArray().Count ?? 0;
                            }
                            if (contentJson.ContainsKey("tracks") && contentJson["tracks"] is JsonArray trk)
                            {
                                trackCount = trk.Count;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual JSON parse errors for corrupted drafts
                    }

                    result.Add(new CapCutProjectTemplateInfo
                    {
                        Name = projName,
                        FolderPath = dir,
                        TracksCount = trackCount,
                        TextsCount = textCount,
                        StickersCount = stickerCount,
                        AudiosCount = audioCount,
                        LastModified = dirInfo.LastWriteTime
                    });
                }
                catch
                {
                    // Ignore directory read error
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error scanning CapCut drafts in {Dir}", draftsRoot);
        }

        return result.OrderByDescending(t => t.LastModified).ToList();
    }

    public async Task<CapCutExportResult> ExportMultiTimelineProjectAsync(
        string projectName,
        IReadOnlyList<CapCutExportItem> items,
        string? targetDraftsRootDir = null,
        string? templateFolderPath = null,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0)
        {
            return new CapCutExportResult
            {
                Success = false,
                ErrorMessage = "Danh sách video và voice rỗng. Vui lòng thêm ít nhất một video + voice để xuất dự án CapCut."
            };
        }

        var draftsRoot = !string.IsNullOrWhiteSpace(targetDraftsRootDir)
            ? targetDraftsRootDir
            : (DetectCapCutDraftsRootDirectory() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CapCut", "User Data", "Projects", "com.lveditor.draft"));

        try
        {
            Directory.CreateDirectory(draftsRoot);

            var cleanProjectName = string.IsNullOrWhiteSpace(projectName)
                ? $"AutoEdit_{DateTime.Now:yyyyMMdd_HHmmss}"
                : SanitizeFileName(projectName);

            var projectDir = Path.Combine(draftsRoot, cleanProjectName);
            int duplicateCounter = 1;
            while (Directory.Exists(projectDir))
            {
                cleanProjectName = $"{SanitizeFileName(projectName)} ({duplicateCounter++})";
                projectDir = Path.Combine(draftsRoot, cleanProjectName);
            }

            Directory.CreateDirectory(projectDir);
            var timelinesDir = Path.Combine(projectDir, "Timelines");
            Directory.CreateDirectory(timelinesDir);

            var nowTimestampUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            var projectGuid = Guid.NewGuid().ToString().ToUpperInvariant();

            // Check if a template project is selected
            JsonObject? templateDraftJson = null;
            long templateOrigDurUs = 10_000_000;
            string templateNameUsed = string.Empty;

            if (!string.IsNullOrWhiteSpace(templateFolderPath) && Directory.Exists(templateFolderPath))
            {
                var tContentPath = Path.Combine(templateFolderPath, "draft_content.json");
                if (File.Exists(tContentPath))
                {
                    try
                    {
                        templateDraftJson = JsonNode.Parse(await File.ReadAllTextAsync(tContentPath, cancellationToken).ConfigureAwait(false))?.AsObject();
                        if (templateDraftJson != null)
                        {
                            templateOrigDurUs = templateDraftJson["duration"]?.GetValue<long>() ?? 10_000_000;
                            templateNameUsed = Path.GetFileName(templateFolderPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to parse template draft JSON from {Path}. Proceeding with standard layout.", templateFolderPath);
                        templateDraftJson = null;
                    }
                }
            }

            var timelineList = new List<JsonObject>();
            string firstTimelineContentJson = string.Empty;
            string mainTimelineGuid = string.Empty;

            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = items[i];
                var timelineGuid = Guid.NewGuid().ToString().ToUpperInvariant();
                var timelineName = $"Dòng thời gian {i + 1:D2}";

                if (i == 0)
                {
                    mainTimelineGuid = timelineGuid;
                }

                // 1. Probe durations if missing
                var videoDurSec = item.VideoDurationSeconds;
                if (videoDurSec <= 0 && File.Exists(item.VideoPath))
                {
                    try
                    {
                        var vMeta = await _probeService.ProbeFileAsync(item.VideoPath, cancellationToken).ConfigureAwait(false);
                        videoDurSec = vMeta.DurationSeconds;
                    }
                    catch
                    {
                        videoDurSec = 10.0;
                    }
                }

                var voiceDurSec = item.VoiceDurationSeconds;
                if (voiceDurSec <= 0 && File.Exists(item.VoicePath))
                {
                    try
                    {
                        var aMeta = await _probeService.ProbeFileAsync(item.VoicePath, cancellationToken).ConfigureAwait(false);
                        voiceDurSec = aMeta.DurationSeconds;
                    }
                    catch
                    {
                        voiceDurSec = videoDurSec;
                    }
                }

                var videoDurationUs = (long)(videoDurSec * 1_000_000);
                var voiceDurationUs = (long)(voiceDurSec * 1_000_000);

                var vTrimStartUs = (long)(Math.Max(0.0, item.VideoTrimStartSeconds) * 1_000_000);
                var vTrimEndUs = (long)(Math.Max(0.0, item.VideoTrimEndSeconds) * 1_000_000);
                var effectiveVideoDurUs = Math.Max(500_000, videoDurationUs - vTrimStartUs - vTrimEndUs);

                var voiceTrimStartUs = (long)(Math.Max(0.0, item.VoiceTrimStartSeconds) * 1_000_000);
                var voiceTrimEndUs = (long)(Math.Max(0.0, item.VoiceTrimEndSeconds) * 1_000_000);
                var effectiveVoiceDurUs = Math.Max(500_000, voiceDurationUs - voiceTrimStartUs - voiceTrimEndUs);

                var extraEndUs = (long)(Math.Max(0.0, item.ExtraEndPaddingSeconds) * 1_000_000);
                var masterDurationUs = effectiveVoiceDurUs + extraEndUs;

                // 2. Build Timeline Directory & draft_content.json
                var timelineDir = Path.Combine(timelinesDir, timelineGuid);
                Directory.CreateDirectory(timelineDir);

                JsonObject timelineContent;
                if (templateDraftJson != null)
                {
                    timelineContent = BuildTimelineDraftContentFromTemplate(
                        templateDraftJson,
                        templateOrigDurUs,
                        timelineGuid,
                        timelineName,
                        item.VideoPath,
                        item.VoicePath,
                        videoDurationUs,
                        voiceDurationUs,
                        vTrimStartUs,
                        effectiveVideoDurUs,
                        voiceTrimStartUs,
                        effectiveVoiceDurUs,
                        masterDurationUs,
                        item.MuteOriginalAudio,
                        item.TransitionCount,
                        item.TransitionType,
                        nowTimestampUs);
                }
                else
                {
                    timelineContent = BuildTimelineDraftContent(
                        timelineGuid,
                        timelineName,
                        item.VideoPath,
                        item.VoicePath,
                        videoDurationUs,
                        voiceDurationUs,
                        vTrimStartUs,
                        effectiveVideoDurUs,
                        voiceTrimStartUs,
                        effectiveVoiceDurUs,
                        masterDurationUs,
                        item.MuteOriginalAudio,
                        item.TransitionCount,
                        item.TransitionType,
                        nowTimestampUs);
                }

                var timelineJsonStr = timelineContent.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(Path.Combine(timelineDir, "draft_content.json"), timelineJsonStr, cancellationToken).ConfigureAwait(false);

                if (i == 0)
                {
                    firstTimelineContentJson = timelineJsonStr;
                }

                timelineList.Add(new JsonObject
                {
                    ["create_time"] = nowTimestampUs,
                    ["id"] = timelineGuid,
                    ["is_marked_delete"] = false,
                    ["name"] = timelineName,
                    ["update_time"] = nowTimestampUs
                });
            }

            // 3. Write Timelines/project.json
            var projectTimelinesJson = new JsonObject
            {
                ["config"] = new JsonObject
                {
                    ["color_space"] = -1,
                    ["mixed_track_mode_on"] = false,
                    ["render_index_track_mode_on"] = false,
                    ["use_float_render"] = false
                },
                ["create_time"] = nowTimestampUs,
                ["id"] = projectGuid,
                ["main_timeline_id"] = mainTimelineGuid,
                ["timelines"] = new JsonArray(timelineList.ToArray()),
                ["update_time"] = nowTimestampUs,
                ["version"] = 0
            };

            await File.WriteAllTextAsync(
                Path.Combine(timelinesDir, "project.json"),
                projectTimelinesJson.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
                cancellationToken).ConfigureAwait(false);

            // 4. Write Root draft_content.json (Default timeline)
            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "draft_content.json"),
                firstTimelineContentJson,
                cancellationToken).ConfigureAwait(false);

            // 5. Write draft_meta_info.json
            var metaInfoJson = new JsonObject
            {
                ["draft_id"] = projectGuid,
                ["draft_name"] = cleanProjectName,
                ["draft_root_path"] = draftsRoot.Replace("\\", "/"),
                ["draft_cover"] = "",
                ["tm_draft_create"] = nowTimestampUs,
                ["tm_draft_modified"] = nowTimestampUs,
                ["draft_timeline_materials_size"] = null
            };

            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "draft_meta_info.json"),
                metaInfoJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);

            // 6. Register into CapCut's root_meta_info.json (Home projects list)
            await RegisterInRootMetaInfoAsync(draftsRoot, cleanProjectName, projectGuid, nowTimestampUs, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Successfully exported CapCut Multi-Timeline project '{ProjectName}' (Template: {Template}) with {Count} timelines to {Dir}",
                cleanProjectName, templateNameUsed, items.Count, projectDir);

            return new CapCutExportResult
            {
                Success = true,
                ProjectName = cleanProjectName,
                ProjectDirectory = projectDir,
                TimelinesCount = items.Count,
                TemplateUsed = templateNameUsed
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export CapCut project '{ProjectName}'", projectName);
            return new CapCutExportResult
            {
                Success = false,
                ErrorMessage = $"Lỗi khi tạo dự án CapCut: {ex.Message}"
            };
        }
    }

    private static JsonObject BuildTimelineDraftContentFromTemplate(
        JsonObject templateDraftJson,
        long templateOrigDurUs,
        string timelineId,
        string timelineName,
        string videoPath,
        string voicePath,
        long videoDurationUs,
        long voiceDurationUs,
        long vTrimStartUs,
        long effectiveVideoDurUs,
        long voiceTrimStartUs,
        long effectiveVoiceDurUs,
        long masterDurationUs,
        bool muteOriginalAudio,
        int transitionCount,
        AutoVideoEditor.Core.Enums.TransitionType transitionType,
        long timestampUs)
    {
        // Deep clone template json
        var clonedJsonStr = templateDraftJson.ToJsonString();
        var root = JsonNode.Parse(clonedJsonStr)?.AsObject() ?? new JsonObject();

        var finalDurationUs = masterDurationUs > 0 ? masterDurationUs : effectiveVideoDurUs;

        root["id"] = timelineId;
        root["name"] = timelineName;
        root["duration"] = finalDurationUs;
        root["create_time"] = timestampUs;
        root["update_time"] = timestampUs;

        if (root.ContainsKey("config") && root["config"] is JsonObject cfg)
        {
            cfg["video_mute"] = muteOriginalAudio;
        }

        var vidMatId = Guid.NewGuid().ToString().ToUpperInvariant();
        var audMatId = Guid.NewGuid().ToString().ToUpperInvariant();
        var speedVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var speedAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var canvasId = Guid.NewGuid().ToString().ToUpperInvariant();
        var scmVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var scmAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var vocVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var vocAudId = Guid.NewGuid().ToString().ToUpperInvariant();

        var cleanVidPath = videoPath.Replace("\\", "/");
        var cleanVoicePath = voicePath.Replace("\\", "/");
        var vidFileName = Path.GetFileName(videoPath);
        var voiceFileName = Path.GetFileName(voicePath);

        // Add new materials
        if (!root.ContainsKey("materials") || root["materials"] is not JsonObject materials)
        {
            materials = new JsonObject();
            root["materials"] = materials;
        }

        if (!materials.ContainsKey("videos") || materials["videos"] is not JsonArray matVideos)
        {
            matVideos = new JsonArray();
            materials["videos"] = matVideos;
        }
        matVideos.Add(new JsonObject
        {
            ["category_id"] = "",
            ["category_name"] = "local",
            ["check_flag"] = 63487,
            ["crop"] = new JsonObject
            {
                ["lower_left_x"] = 0.0,
                ["lower_left_y"] = 1.0,
                ["lower_right_x"] = 1.0,
                ["lower_right_y"] = 1.0,
                ["upper_left_x"] = 0.0,
                ["upper_left_y"] = 0.0,
                ["upper_right_x"] = 1.0,
                ["upper_right_y"] = 0.0
            },
            ["crop_ratio"] = "free",
            ["crop_scale"] = 1.0,
            ["duration"] = videoDurationUs,
            ["extra_type"] = "option_empty",
            ["id"] = vidMatId,
            ["material_name"] = vidFileName,
            ["path"] = cleanVidPath,
            ["type"] = "video"
        });

        if (!materials.ContainsKey("audios") || materials["audios"] is not JsonArray matAudios)
        {
            matAudios = new JsonArray();
            materials["audios"] = matAudios;
        }
        matAudios.Add(new JsonObject
        {
            ["category_id"] = "",
            ["category_name"] = "local",
            ["check_flag"] = 1,
            ["duration"] = voiceDurationUs,
            ["id"] = audMatId,
            ["material_name"] = voiceFileName,
            ["name"] = voiceFileName,
            ["path"] = cleanVoicePath,
            ["type"] = "music"
        });

        if (!materials.ContainsKey("canvases") || materials["canvases"] is not JsonArray matCanvases)
        {
            matCanvases = new JsonArray();
            materials["canvases"] = matCanvases;
        }
        matCanvases.Add(new JsonObject
        {
            ["id"] = canvasId,
            ["type"] = "canvas_color",
            ["color"] = "#000000"
        });

        if (!materials.ContainsKey("speeds") || materials["speeds"] is not JsonArray matSpeeds)
        {
            matSpeeds = new JsonArray();
            materials["speeds"] = matSpeeds;
        }
        matSpeeds.Add(new JsonObject { ["curve_speed"] = null, ["id"] = speedVidId, ["mode"] = 0, ["speed"] = 1.0, ["type"] = "speed" });
        matSpeeds.Add(new JsonObject { ["curve_speed"] = null, ["id"] = speedAudId, ["mode"] = 0, ["speed"] = 1.0, ["type"] = "speed" });

        if (!materials.ContainsKey("sound_channel_mappings") || materials["sound_channel_mappings"] is not JsonArray matScm)
        {
            matScm = new JsonArray();
            materials["sound_channel_mappings"] = matScm;
        }
        matScm.Add(new JsonObject { ["audio_channel_mapping"] = 0, ["id"] = scmVidId, ["is_config_open"] = false, ["type"] = "" });
        matScm.Add(new JsonObject { ["audio_channel_mapping"] = 0, ["id"] = scmAudId, ["is_config_open"] = false, ["type"] = "none" });

        if (!materials.ContainsKey("vocal_separations") || materials["vocal_separations"] is not JsonArray matVoc)
        {
            matVoc = new JsonArray();
            materials["vocal_separations"] = matVoc;
        }
        matVoc.Add(new JsonObject { ["choice"] = 0, ["id"] = vocVidId, ["removed_sounds"] = new JsonArray(), ["type"] = "vocal_separation" });
        matVoc.Add(new JsonObject { ["choice"] = 0, ["id"] = vocAudId, ["removed_sounds"] = new JsonArray(), ["type"] = "vocal_separation" });

        // Build main video segments with loop / cut to fit master duration
        var newVideoSegments = BuildVideoSegments(
            vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
            vTrimStartUs, effectiveVideoDurUs, finalDurationUs,
            muteOriginalAudio, transitionCount, transitionType);

        var newVoiceSegments = new JsonArray(
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                ["material_id"] = audMatId,
                ["render_index"] = 0,
                ["source_timerange"] = new JsonObject
                {
                    ["start"] = voiceTrimStartUs,
                    ["duration"] = effectiveVoiceDurUs
                },
                ["target_timerange"] = new JsonObject
                {
                    ["start"] = 0,
                    ["duration"] = effectiveVoiceDurUs
                },
                ["render_timerange"] = new JsonObject
                {
                    ["start"] = 0,
                    ["duration"] = 0
                },
                ["speed"] = 1.0,
                ["volume"] = 1.0,
                ["visible"] = true,
                ["state"] = 0,
                ["extra_material_refs"] = new JsonArray(speedAudId, scmAudId, vocAudId)
            }
        );

        // Adjust tracks
        bool replacedMainVideoTrack = false;
        bool replacedMainAudioTrack = false;
        double durationScaleRatio = (double)finalDurationUs / Math.Max(1_000_000, templateOrigDurUs);

        if (root.ContainsKey("tracks") && root["tracks"] is JsonArray tracks)
        {
            foreach (var tNode in tracks)
            {
                if (tNode is not JsonObject track) continue;
                var trackType = track["type"]?.ToString();
                var trackFlag = track["flag"]?.GetValue<int>() ?? 0;

                // Main video track replacement (flag 0 and first video track)
                if (trackType == "video" && trackFlag == 0 && !replacedMainVideoTrack)
                {
                    track["segments"] = newVideoSegments;
                    replacedMainVideoTrack = true;
                    continue;
                }

                // Main voice track replacement (flag 0 and first audio track)
                if (trackType == "audio" && trackFlag == 0 && !replacedMainAudioTrack)
                {
                    track["segments"] = newVoiceSegments;
                    replacedMainAudioTrack = true;
                    continue;
                }

                // Co-scale auxiliary tracks (Text, Sticker, Effects, Secondary BGM)
                if (track.ContainsKey("segments") && track["segments"] is JsonArray segArray)
                {
                    foreach (var segNode in segArray)
                    {
                        if (segNode is not JsonObject seg) continue;
                        if (seg.ContainsKey("target_timerange") && seg["target_timerange"] is JsonObject tt)
                        {
                            long origStart = tt["start"]?.GetValue<long>() ?? 0;
                            long origDur = tt["duration"]?.GetValue<long>() ?? 0;

                            // If segment spanned across the whole template duration, stretch it to full finalDurationUs
                            if (origStart <= 100_000 && (origStart + origDur) >= (templateOrigDurUs - 300_000))
                            {
                                tt["start"] = 0;
                                tt["duration"] = finalDurationUs;
                                if (seg.ContainsKey("source_timerange") && seg["source_timerange"] is JsonObject st)
                                {
                                    st["start"] = 0;
                                    st["duration"] = finalDurationUs;
                                }
                            }
                            else
                            {
                                long newStart = (long)(origStart * durationScaleRatio);
                                long newDur = Math.Min((long)(origDur * durationScaleRatio), Math.Max(500_000, finalDurationUs - newStart));
                                tt["start"] = newStart;
                                tt["duration"] = Math.Max(300_000, newDur);
                                if (seg.ContainsKey("source_timerange") && seg["source_timerange"] is JsonObject st)
                                {
                                    st["duration"] = Math.Max(300_000, newDur);
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: If no video track was present in template
            if (!replacedMainVideoTrack)
            {
                tracks.Insert(0, new JsonObject
                {
                    ["attribute"] = 0,
                    ["flag"] = 0,
                    ["id"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                    ["is_default_name"] = true,
                    ["name"] = "",
                    ["type"] = "video",
                    ["segments"] = newVideoSegments
                });
            }

            // Fallback: If no audio track was present in template
            if (!replacedMainAudioTrack)
            {
                tracks.Add(new JsonObject
                {
                    ["attribute"] = 0,
                    ["flag"] = 0,
                    ["id"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                    ["is_default_name"] = true,
                    ["name"] = "",
                    ["type"] = "audio",
                    ["segments"] = newVoiceSegments
                });
            }
        }

        return root;
    }

    private static JsonObject BuildTimelineDraftContent(
        string timelineId,
        string timelineName,
        string videoPath,
        string voicePath,
        long videoDurationUs,
        long voiceDurationUs,
        long vTrimStartUs,
        long effectiveVideoDurUs,
        long voiceTrimStartUs,
        long effectiveVoiceDurUs,
        long masterDurationUs,
        bool muteOriginalAudio,
        int transitionCount,
        AutoVideoEditor.Core.Enums.TransitionType transitionType,
        long timestampUs)
    {
        var vidMatId = Guid.NewGuid().ToString().ToUpperInvariant();
        var audMatId = Guid.NewGuid().ToString().ToUpperInvariant();
        var speedVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var speedAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var canvasId = Guid.NewGuid().ToString().ToUpperInvariant();
        var scmVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var scmAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var vocVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var vocAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var segVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var segAudId = Guid.NewGuid().ToString().ToUpperInvariant();
        var trackVidId = Guid.NewGuid().ToString().ToUpperInvariant();
        var trackAudId = Guid.NewGuid().ToString().ToUpperInvariant();

        var cleanVidPath = videoPath.Replace("\\", "/");
        var cleanVoicePath = voicePath.Replace("\\", "/");
        var vidFileName = Path.GetFileName(videoPath);
        var voiceFileName = Path.GetFileName(voicePath);

        var finalDurationUs = masterDurationUs > 0 ? masterDurationUs : effectiveVideoDurUs;

        var videoSegments = BuildVideoSegments(
            vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
            vTrimStartUs, effectiveVideoDurUs, finalDurationUs,
            muteOriginalAudio, transitionCount, transitionType);

        var root = new JsonObject
        {
            ["canvas_config"] = new JsonObject
            {
                ["height"] = 1920,
                ["ratio"] = "original",
                ["width"] = 1080
            },
            ["color_space"] = 0,
            ["config"] = new JsonObject
            {
                ["adjust_max_index"] = 1,
                ["attachment_info"] = new JsonArray(),
                ["combination_max_index"] = 1,
                ["export_range"] = null,
                ["extract_audio_last_index"] = 1,
                ["lyrics_recognition_id"] = "",
                ["lyrics_sync"] = true,
                ["lyrics_taskinfo"] = new JsonArray(),
                ["maintrack_adsorb"] = true,
                ["material_save_mode"] = 0,
                ["multi_camera_mode"] = false,
                ["original_sound_last_index"] = 1,
                ["record_audio_last_index"] = 1,
                ["sticker_max_index"] = 1,
                ["subtitle_keywords_config"] = null,
                ["subtitle_recognition_id"] = "",
                ["subtitle_sync"] = true,
                ["subtitle_taskinfo"] = new JsonArray(),
                ["system_font_list"] = new JsonArray(),
                ["video_mute"] = muteOriginalAudio,
                ["zoom_info_params"] = null
            },
            ["cover"] = null,
            ["create_time"] = timestampUs,
            ["draft_type"] = "video",
            ["duration"] = finalDurationUs,
            ["extra_info"] = null,
            ["fps"] = 30.0,
            ["free_render_index_mode_on"] = false,
            ["function_assistant_info"] = new JsonObject
            {
                ["auto_adjust"] = false,
                ["auto_caption"] = false,
                ["color_correction"] = false,
                ["deflicker_segid_list"] = new JsonArray(),
                ["enhande_voice"] = false,
                ["enhance_quality"] = false,
                ["normalize_loudness"] = false,
                ["retouch"] = false,
                ["smart_rec_applied"] = false,
                ["smooth_slow_motion"] = false
            },
            ["group_container"] = null,
            ["id"] = timelineId,
            ["is_drop_frame_timecode"] = false,
            ["keyframes"] = new JsonObject
            {
                ["adjusts"] = new JsonArray(),
                ["audios"] = new JsonArray(),
                ["effects"] = new JsonArray(),
                ["filters"] = new JsonArray(),
                ["handwrites"] = new JsonArray(),
                ["stickers"] = new JsonArray(),
                ["texts"] = new JsonArray(),
                ["videos"] = new JsonArray()
            },
            ["keyframe_graph_list"] = new JsonArray(),
            ["last_modified_platform"] = new JsonObject
            {
                ["app_id"] = 359289,
                ["app_source"] = "cc",
                ["app_version"] = "9.3.0",
                ["device_id"] = "",
                ["hard_disk_id"] = "",
                ["mac_address"] = "",
                ["os"] = "windows",
                ["os_version"] = "10.0.26200"
            },
            ["lyrics_effects"] = new JsonArray(),
            ["materials"] = new JsonObject
            {
                ["ai_text_effects"] = new JsonArray(),
                ["ai_translates"] = new JsonArray(),
                ["audio_balances"] = new JsonArray(),
                ["audio_effects"] = new JsonArray(),
                ["audio_fades"] = new JsonArray(),
                ["audio_pannings"] = new JsonArray(),
                ["audio_pitch_shifts"] = new JsonArray(),
                ["audio_track_indexes"] = new JsonArray(),
                ["audios"] = new JsonArray(
                    new JsonObject
                    {
                        ["app_id"] = 0,
                        ["category_id"] = "",
                        ["category_name"] = "local",
                        ["check_flag"] = 1,
                        ["duration"] = voiceDurationUs,
                        ["id"] = audMatId,
                        ["music_id"] = Guid.NewGuid().ToString(),
                        ["name"] = voiceFileName,
                        ["path"] = cleanVoicePath,
                        ["type"] = "extract_music",
                        ["unique_id"] = Guid.NewGuid().ToString("N")
                    }
                ),
                ["beats"] = new JsonArray(),
                ["canvases"] = new JsonArray(
                    new JsonObject
                    {
                        ["album_image"] = "",
                        ["blur"] = 0.0,
                        ["color"] = "",
                        ["id"] = canvasId,
                        ["image"] = "",
                        ["image_id"] = "",
                        ["image_name"] = "",
                        ["source_platform"] = 0,
                        ["team_id"] = "",
                        ["type"] = "canvas_color"
                    }
                ),
                ["chromas"] = new JsonArray(),
                ["color_curves"] = new JsonArray(),
                ["common_mask"] = new JsonArray(),
                ["digital_human_model_dressing"] = new JsonArray(),
                ["digital_humans"] = new JsonArray(),
                ["drafts"] = new JsonArray(),
                ["effects"] = new JsonArray(),
                ["green_screens"] = new JsonArray(),
                ["handwrites"] = new JsonArray(),
                ["hsl"] = new JsonArray(),
                ["hsl_curves"] = new JsonArray(),
                ["images"] = new JsonArray(),
                ["log_color_wheels"] = new JsonArray(),
                ["loudnesses"] = new JsonArray(),
                ["manual_beautys"] = new JsonArray(),
                ["manual_deformations"] = new JsonArray(),
                ["material_animations"] = new JsonArray(),
                ["material_colors"] = new JsonArray(),
                ["placeholder_infos"] = new JsonArray(),
                ["placeholders"] = new JsonArray(),
                ["plugin_effects"] = new JsonArray(),
                ["primary_color_wheels"] = new JsonArray(),
                ["realtime_denoises"] = new JsonArray(),
                ["shapes"] = new JsonArray(),
                ["smart_crops"] = new JsonArray(),
                ["smart_relights"] = new JsonArray(),
                ["sound_channel_mappings"] = new JsonArray(
                    new JsonObject { ["audio_channel_mapping"] = 0, ["id"] = scmVidId, ["is_config_open"] = false, ["type"] = "" },
                    new JsonObject { ["audio_channel_mapping"] = 0, ["id"] = scmAudId, ["is_config_open"] = false, ["type"] = "none" }
                ),
                ["speeds"] = new JsonArray(
                    new JsonObject { ["curve_speed"] = null, ["id"] = speedVidId, ["mode"] = 0, ["speed"] = 1.0, ["type"] = "speed" },
                    new JsonObject { ["curve_speed"] = null, ["id"] = speedAudId, ["mode"] = 0, ["speed"] = 1.0, ["type"] = "speed" }
                ),
                ["stickers"] = new JsonArray(),
                ["tail_leaders"] = new JsonArray(),
                ["text_templates"] = new JsonArray(),
                ["texts"] = new JsonArray(),
                ["time_marks"] = new JsonArray(),
                ["transitions"] = new JsonArray(),
                ["video_effects"] = new JsonArray(),
                ["video_radius"] = new JsonArray(),
                ["video_shadows"] = new JsonArray(),
                ["video_strokes"] = new JsonArray(),
                ["video_trackings"] = new JsonArray(),
                ["videos"] = new JsonArray(
                    new JsonObject
                    {
                        ["category_id"] = "",
                        ["category_name"] = "local",
                        ["check_flag"] = 62978047,
                        ["crop_scale"] = 1.0,
                        ["duration"] = videoDurationUs,
                        ["height"] = 1920,
                        ["width"] = 1080,
                        ["id"] = vidMatId,
                        ["local_material_id"] = Guid.NewGuid().ToString(),
                        ["material_name"] = vidFileName,
                        ["path"] = cleanVidPath,
                        ["type"] = "video"
                    }
                ),
                ["vocal_beautifys"] = new JsonArray(),
                ["vocal_separations"] = new JsonArray(
                    new JsonObject { ["choice"] = 0, ["id"] = vocVidId, ["removed_sounds"] = new JsonArray(), ["type"] = "vocal_separation" },
                    new JsonObject { ["choice"] = 0, ["id"] = vocAudId, ["removed_sounds"] = new JsonArray(), ["type"] = "vocal_separation" }
                )
            },
            ["mixed_track_mode_on"] = false,
            ["mutable_config"] = null,
            ["name"] = timelineName,
            ["new_version"] = "113.0.0",
            ["path"] = "",
            ["platform"] = new JsonObject
            {
                ["app_id"] = 359289,
                ["app_source"] = "cc",
                ["app_version"] = "9.3.0",
                ["device_id"] = "",
                ["hard_disk_id"] = "",
                ["mac_address"] = "",
                ["os"] = "windows",
                ["os_version"] = "10.0.26200"
            },
            ["relationships"] = new JsonArray(),
            ["render_index_track_mode_on"] = true,
            ["retouch_cover"] = null,
            ["smart_ads_info"] = new JsonObject { ["draft_url"] = "", ["page_from"] = "", ["routine"] = "" },
            ["source"] = "default",
            ["static_cover_image_path"] = "",
            ["time_marks"] = null,
            ["tracks"] = new JsonArray(
                // Track 1: Video
                new JsonObject
                {
                    ["attribute"] = 0,
                    ["flag"] = 0,
                    ["id"] = trackVidId,
                    ["is_default_name"] = true,
                    ["name"] = "",
                    ["type"] = "video",
                    ["segments"] = videoSegments
                },
                // Track 2: Audio/Voice
                new JsonObject
                {
                    ["attribute"] = 0,
                    ["flag"] = 0,
                    ["id"] = trackAudId,
                    ["is_default_name"] = true,
                    ["name"] = "",
                    ["type"] = "audio",
                    ["segments"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = segAudId,
                            ["material_id"] = audMatId,
                            ["render_index"] = 0,
                            ["source_timerange"] = new JsonObject
                            {
                                ["start"] = voiceTrimStartUs,
                                ["duration"] = effectiveVoiceDurUs
                            },
                            ["target_timerange"] = new JsonObject
                            {
                                ["start"] = 0,
                                ["duration"] = effectiveVoiceDurUs
                            },
                            ["render_timerange"] = new JsonObject
                            {
                                ["start"] = 0,
                                ["duration"] = 0
                            },
                            ["speed"] = 1.0,
                            ["volume"] = 1.0,
                            ["visible"] = true,
                            ["state"] = 0,
                            ["extra_material_refs"] = new JsonArray(speedAudId, scmAudId, vocAudId)
                        }
                    )
                }
            ),
            ["uneven_animation_template_info"] = new JsonObject
            {
                ["composition"] = "",
                ["content"] = "",
                ["order"] = "",
                ["sub_template_info_list"] = new JsonArray()
            },
            ["update_time"] = timestampUs,
            ["version"] = 360000
        };

        return root;
    }

    private static async Task RegisterInRootMetaInfoAsync(
        string draftsRoot,
        string projectName,
        string projectGuid,
        long timestampUs,
        CancellationToken cancellationToken)
    {
        var rootMetaFile = Path.Combine(draftsRoot, "root_meta_info.json");
        JsonObject rootJson;

        if (File.Exists(rootMetaFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(rootMetaFile, cancellationToken).ConfigureAwait(false);
                rootJson = JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                rootJson = new JsonObject();
            }
        }
        else
        {
            rootJson = new JsonObject();
        }

        if (!rootJson.ContainsKey("all_draft_store") || rootJson["all_draft_store"] is not JsonArray draftStore)
        {
            draftStore = new JsonArray();
            rootJson["all_draft_store"] = draftStore;
        }

        var newEntry = new JsonObject
        {
            ["draft_cover"] = "",
            ["draft_fold_info"] = null,
            ["draft_folder"] = "",
            ["draft_id"] = projectGuid,
            ["draft_is_ban"] = false,
            ["draft_is_collected"] = false,
            ["draft_is_hide"] = false,
            ["draft_is_invisible"] = false,
            ["draft_is_template"] = false,
            ["draft_name"] = projectName,
            ["draft_new_version"] = "113.0.0",
            ["draft_root_path"] = draftsRoot.Replace("\\", "/"),
            ["draft_timeline_materials_size"] = 0,
            ["draft_type"] = "",
            ["tm_draft_cloud_completed"] = "",
            ["tm_draft_cloud_modified"] = 0,
            ["tm_draft_create"] = timestampUs,
            ["tm_draft_modified"] = timestampUs,
            ["tm_draft_removed"] = 0
        };

        // Insert at beginning of all_draft_store so it appears at the top of CapCut's project list
        draftStore.Insert(0, newEntry);

        await File.WriteAllTextAsync(
            rootMetaFile,
            rootJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
    }

    private static JsonArray BuildVideoSegments(
        string vidMatId,
        string speedVidId,
        string canvasId,
        string scmVidId,
        string vocVidId,
        long vTrimStartUs,
        long effectiveVideoDurUs,
        long finalDurationUs,
        bool muteOriginalAudio,
        int transitionCount,
        AutoVideoEditor.Core.Enums.TransitionType transitionType)
    {
        var videoSegments = new JsonArray();
        long currentTargetStart = 0;

        int numCuts = (transitionCount > 0 && transitionType != AutoVideoEditor.Core.Enums.TransitionType.None && effectiveVideoDurUs >= 3_000_000)
            ? Math.Min(transitionCount + 1, 6)
            : 1;

        if (effectiveVideoDurUs >= finalDurationUs)
        {
            if (numCuts > 1)
            {
                long segTargetDur = finalDurationUs / numCuts;
                long segSourceDur = effectiveVideoDurUs / numCuts;
                for (int s = 0; s < numCuts; s++)
                {
                    long thisTargetDur = (s == numCuts - 1) ? (finalDurationUs - currentTargetStart) : segTargetDur;
                    long thisSourceDur = Math.Min(thisTargetDur, segSourceDur);
                    long thisSourceStart = vTrimStartUs + (s * segSourceDur);

                    videoSegments.Add(CreateVideoSegment(
                        vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
                        thisSourceStart, thisSourceDur, currentTargetStart, thisTargetDur,
                        muteOriginalAudio));

                    currentTargetStart += thisTargetDur;
                }
            }
            else
            {
                videoSegments.Add(CreateVideoSegment(
                    vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
                    vTrimStartUs, finalDurationUs, 0, finalDurationUs,
                    muteOriginalAudio));
            }
        }
        else
        {
            // Video is shorter than finalDurationUs: Loop/repeat video to fill voice master duration
            while (currentTargetStart < finalDurationUs)
            {
                long remainingTargetUs = finalDurationUs - currentTargetStart;
                long chunkTargetUs = Math.Min(remainingTargetUs, effectiveVideoDurUs);

                if (numCuts > 1 && chunkTargetUs >= 3_000_000)
                {
                    long subSegDur = chunkTargetUs / numCuts;
                    for (int s = 0; s < numCuts; s++)
                    {
                        long thisTargetDur = (s == numCuts - 1) ? (chunkTargetUs - (s * subSegDur)) : subSegDur;
                        long thisSourceStart = vTrimStartUs + (s * subSegDur);

                        videoSegments.Add(CreateVideoSegment(
                            vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
                            thisSourceStart, thisTargetDur, currentTargetStart, thisTargetDur,
                            muteOriginalAudio));

                        currentTargetStart += thisTargetDur;
                    }
                }
                else
                {
                    videoSegments.Add(CreateVideoSegment(
                        vidMatId, speedVidId, canvasId, scmVidId, vocVidId,
                        vTrimStartUs, chunkTargetUs, currentTargetStart, chunkTargetUs,
                        muteOriginalAudio));

                    currentTargetStart += chunkTargetUs;
                }
            }
        }

        return videoSegments;
    }

    private static JsonObject CreateVideoSegment(
        string vidMatId,
        string speedVidId,
        string canvasId,
        string scmVidId,
        string vocVidId,
        long sourceStartUs,
        long sourceDurUs,
        long targetStartUs,
        long targetDurUs,
        bool muteOriginalAudio)
    {
        return new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString().ToUpperInvariant(),
            ["material_id"] = vidMatId,
            ["render_index"] = 0,
            ["source_timerange"] = new JsonObject
            {
                ["start"] = sourceStartUs,
                ["duration"] = sourceDurUs
            },
            ["target_timerange"] = new JsonObject
            {
                ["start"] = targetStartUs,
                ["duration"] = targetDurUs
            },
            ["render_timerange"] = new JsonObject
            {
                ["start"] = 0,
                ["duration"] = 0
            },
            ["speed"] = 1.0,
            ["volume"] = muteOriginalAudio ? 0.0 : 1.0,
            ["visible"] = true,
            ["state"] = 0,
            ["extra_material_refs"] = new JsonArray(speedVidId, canvasId, scmVidId, vocVidId)
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
    }
}

