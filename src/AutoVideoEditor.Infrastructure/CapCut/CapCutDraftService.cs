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

    public async Task<CapCutExportResult> ExportMultiTimelineProjectAsync(
        string projectName,
        IReadOnlyList<CapCutExportItem> items,
        string? targetDraftsRootDir = null,
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

                var timelineContent = BuildTimelineDraftContent(
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

            _logger?.LogInformation("Successfully exported CapCut Multi-Timeline project '{ProjectName}' with {Count} timelines to {Dir}",
                cleanProjectName, items.Count, projectDir);

            return new CapCutExportResult
            {
                Success = true,
                ProjectName = cleanProjectName,
                ProjectDirectory = projectDir,
                TimelinesCount = items.Count
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

        var finalDurationUs = Math.Max(effectiveVideoDurUs, masterDurationUs);

        var videoSegments = new JsonArray();
        int numCuts = (transitionCount > 0 && transitionType != AutoVideoEditor.Core.Enums.TransitionType.None && effectiveVideoDurUs >= 3_000_000)
            ? Math.Min(transitionCount + 1, 6)
            : 1;

        if (numCuts > 1)
        {
            long segDur = effectiveVideoDurUs / numCuts;
            long currentTargetStart = 0;
            for (int s = 0; s < numCuts; s++)
            {
                long thisSegDur = (s == numCuts - 1) ? (effectiveVideoDurUs - currentTargetStart) : segDur;
                long thisSourceStart = vTrimStartUs + (s * segDur);

                videoSegments.Add(new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString().ToUpperInvariant(),
                    ["material_id"] = vidMatId,
                    ["render_index"] = 0,
                    ["source_timerange"] = new JsonObject
                    {
                        ["start"] = thisSourceStart,
                        ["duration"] = thisSegDur
                    },
                    ["target_timerange"] = new JsonObject
                    {
                        ["start"] = currentTargetStart,
                        ["duration"] = thisSegDur
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
                });

                currentTargetStart += thisSegDur;
            }
        }
        else
        {
            videoSegments.Add(new JsonObject
            {
                ["id"] = segVidId,
                ["material_id"] = vidMatId,
                ["render_index"] = 0,
                ["source_timerange"] = new JsonObject
                {
                    ["start"] = vTrimStartUs,
                    ["duration"] = effectiveVideoDurUs
                },
                ["target_timerange"] = new JsonObject
                {
                    ["start"] = 0,
                    ["duration"] = effectiveVideoDurUs
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
            });
        }

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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
    }
}
