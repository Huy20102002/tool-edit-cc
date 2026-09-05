using System.Text.Json.Nodes;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Core.Models;
using AutoVideoEditor.Infrastructure.CapCut;
using Xunit;

namespace AutoVideoEditor.Tests;

public class FakeFFprobeService : IFFprobeService
{
    public Task<MediaFileInfo> ProbeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MediaFileInfo
        {
            DurationSeconds = 30.0,
            Width = 1080,
            Height = 1920,
            Fps = 30.0
        });
    }
}

public class CapCutDraftServiceTests
{
    private readonly CapCutDraftService _service;
    private readonly string _testTempDir;

    public CapCutDraftServiceTests()
    {
        _service = new CapCutDraftService(new FakeFFprobeService());
        _testTempDir = Path.Combine(Path.GetTempPath(), "CapCutTests_" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task ExportMultiTimelineProject_EmptyList_ReturnsError()
    {
        var result = await _service.ExportMultiTimelineProjectAsync("TestProject", new List<CapCutExportItem>(), _testTempDir);
        Assert.False(result.Success);
        Assert.Contains("rỗng", result.ErrorMessage);
    }

    [Fact]
    public async Task ExportMultiTimelineProject_ValidItems_CreatesMultiTimelineStructure()
    {
        var items = new List<CapCutExportItem>
        {
            new()
            {
                OrderIndex = 1,
                VideoPath = @"C:\media\video1.mp4",
                VoicePath = @"C:\media\voice1.mp3",
                VideoDurationSeconds = 45.0,
                VoiceDurationSeconds = 25.0,
                VideoTrimStartSeconds = 1.0,
                VideoTrimEndSeconds = 0.5
            },
            new()
            {
                OrderIndex = 2,
                VideoPath = @"C:\media\video2.mp4",
                VoicePath = @"C:\media\voice2.mp3",
                VideoDurationSeconds = 30.0,
                VoiceDurationSeconds = 30.0
            }
        };

        var result = await _service.ExportMultiTimelineProjectAsync("MyCapCutProject", items, _testTempDir);

        Assert.True(result.Success);
        Assert.Equal(2, result.TimelinesCount);
        Assert.True(Directory.Exists(result.ProjectDirectory));

        // Verify Timelines/project.json
        var projectJsonPath = Path.Combine(result.ProjectDirectory, "Timelines", "project.json");
        Assert.True(File.Exists(projectJsonPath));
        var projectJsonContent = await File.ReadAllTextAsync(projectJsonPath);
        var projectJson = JsonNode.Parse(projectJsonContent)?.AsObject();
        Assert.NotNull(projectJson);
        Assert.True(projectJson.ContainsKey("timelines"));
        var timelines = projectJson["timelines"]?.AsArray();
        Assert.NotNull(timelines);
        Assert.Equal(2, timelines.Count);
        Assert.Equal("Dòng thời gian 01", timelines[0]?["name"]?.ToString());
        Assert.Equal("Dòng thời gian 02", timelines[1]?["name"]?.ToString());

        // Verify Root draft_content.json
        var rootContentPath = Path.Combine(result.ProjectDirectory, "draft_content.json");
        Assert.True(File.Exists(rootContentPath));
        var rootJson = JsonNode.Parse(await File.ReadAllTextAsync(rootContentPath))?.AsObject();
        Assert.NotNull(rootJson);
        Assert.True(rootJson.ContainsKey("tracks"));
        var tracks = rootJson["tracks"]?.AsArray();
        Assert.NotNull(tracks);
        Assert.Equal(2, tracks.Count); // Video and Audio tracks

        // Verify draft_meta_info.json
        var metaPath = Path.Combine(result.ProjectDirectory, "draft_meta_info.json");
        Assert.True(File.Exists(metaPath));

        // Verify root_meta_info.json in root drafts dir
        var rootMetaPath = Path.Combine(_testTempDir, "root_meta_info.json");
        Assert.True(File.Exists(rootMetaPath));

        // Cleanup
        if (Directory.Exists(_testTempDir))
        {
            Directory.Delete(_testTempDir, true);
        }
    }

    [Fact]
    public async Task GetAvailableTemplates_ScansDraftsDir_ReturnsTemplatesInfo()
    {
        var draftsRoot = Path.Combine(_testTempDir, "TemplatesTestRoot");
        var templateDir = Path.Combine(draftsRoot, "SampleTemplate_01");
        Directory.CreateDirectory(templateDir);

        // Create draft_meta_info.json
        var metaJson = new JsonObject
        {
            ["draft_name"] = "Mẫu Bán Hàng TikTok Pro"
        };
        await File.WriteAllTextAsync(Path.Combine(templateDir, "draft_meta_info.json"), metaJson.ToJsonString());

        // Create draft_content.json with materials and tracks
        var contentJson = new JsonObject
        {
            ["duration"] = 15_000_000,
            ["materials"] = new JsonObject
            {
                ["texts"] = new JsonArray { new JsonObject { ["id"] = "t1", ["content"] = "Tiêu đề số 1" } },
                ["stickers"] = new JsonArray { new JsonObject { ["id"] = "s1" }, new JsonObject { ["id"] = "s2" } },
                ["audios"] = new JsonArray { new JsonObject { ["id"] = "a1" }, new JsonObject { ["id"] = "bgm1" } },
                ["videos"] = new JsonArray { new JsonObject { ["id"] = "v1" } }
            },
            ["tracks"] = new JsonArray
            {
                new JsonObject { ["id"] = "trk_v", ["type"] = "video", ["flag"] = 0, ["segments"] = new JsonArray() },
                new JsonObject { ["id"] = "trk_a", ["type"] = "audio", ["flag"] = 0, ["segments"] = new JsonArray() },
                new JsonObject { ["id"] = "trk_bgm", ["type"] = "audio", ["flag"] = 1, ["segments"] = new JsonArray() },
                new JsonObject { ["id"] = "trk_t", ["type"] = "text", ["flag"] = 1, ["segments"] = new JsonArray() },
                new JsonObject { ["id"] = "trk_s", ["type"] = "sticker", ["flag"] = 0, ["segments"] = new JsonArray() }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(templateDir, "draft_content.json"), contentJson.ToJsonString());

        var templates = _service.GetAvailableTemplates(draftsRoot);

        Assert.Single(templates);
        var t = templates[0];
        Assert.Equal("Mẫu Bán Hàng TikTok Pro", t.Name);
        Assert.Equal(1, t.TextsCount);
        Assert.Equal(2, t.StickersCount);
        Assert.Equal(2, t.AudiosCount);
        Assert.Equal(5, t.TracksCount);
        Assert.Contains("Mẫu Bán Hàng TikTok Pro", t.DisplayName);
        Assert.Contains("1 chữ", t.DisplayName);
        Assert.Contains("2 sticker", t.DisplayName);

        // Cleanup
        if (Directory.Exists(draftsRoot))
        {
            Directory.Delete(draftsRoot, true);
        }
    }

    [Fact]
    public async Task ExportMultiTimelineProject_WithTemplate_ClonesTracksAndScalesDuration()
    {
        var draftsRoot = Path.Combine(_testTempDir, "TemplateExportRoot");
        var templateDir = Path.Combine(draftsRoot, "MyTemplateProj");
        Directory.CreateDirectory(templateDir);

        // 10s template with Text & Sticker & BGM
        var contentJson = new JsonObject
        {
            ["duration"] = 10_000_000,
            ["materials"] = new JsonObject
            {
                ["texts"] = new JsonArray { new JsonObject { ["id"] = "txt_01", ["content"] = "Giảm giá 50%" } },
                ["stickers"] = new JsonArray { new JsonObject { ["id"] = "stk_01" } },
                ["audios"] = new JsonArray
                {
                    new JsonObject { ["id"] = "voice_orig", ["path"] = @"C:\old_voice.mp3" },
                    new JsonObject { ["id"] = "bgm_orig", ["path"] = @"C:\music\bgm.mp3" }
                },
                ["videos"] = new JsonArray { new JsonObject { ["id"] = "vid_orig", ["path"] = @"C:\old_video.mp4" } }
            },
            ["tracks"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "trk_video_main",
                    ["type"] = "video",
                    ["flag"] = 0,
                    ["segments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "seg_v1",
                            ["material_id"] = "vid_orig",
                            ["target_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 },
                            ["source_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 }
                        }
                    }
                },
                new JsonObject
                {
                    ["id"] = "trk_audio_voice",
                    ["type"] = "audio",
                    ["flag"] = 0,
                    ["segments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "seg_a1",
                            ["material_id"] = "voice_orig",
                            ["target_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 },
                            ["source_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 }
                        }
                    }
                },
                new JsonObject
                {
                    ["id"] = "trk_audio_bgm",
                    ["type"] = "audio",
                    ["flag"] = 1,
                    ["segments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "seg_bgm1",
                            ["material_id"] = "bgm_orig",
                            ["target_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 },
                            ["source_timerange"] = new JsonObject { ["start"] = 0, ["duration"] = 10_000_000 }
                        }
                    }
                },
                new JsonObject
                {
                    ["id"] = "trk_text",
                    ["type"] = "text",
                    ["flag"] = 1,
                    ["segments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "seg_t1",
                            ["material_id"] = "txt_01",
                            ["target_timerange"] = new JsonObject { ["start"] = 1_000_000, ["duration"] = 8_000_000 }
                        }
                    }
                },
                new JsonObject
                {
                    ["id"] = "trk_sticker",
                    ["type"] = "sticker",
                    ["flag"] = 0,
                    ["segments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "seg_s1",
                            ["material_id"] = "stk_01",
                            ["target_timerange"] = new JsonObject { ["start"] = 2_000_000, ["duration"] = 6_000_000 }
                        }
                    }
                }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(templateDir, "draft_content.json"), contentJson.ToJsonString());

        var items = new List<CapCutExportItem>
        {
            new()
            {
                OrderIndex = 1,
                VideoPath = @"C:\new_video1.mp4",
                VoicePath = @"C:\new_voice1.mp3",
                VideoDurationSeconds = 40.0,
                VoiceDurationSeconds = 20.0, // 20s = 20,000,000 us (2x template duration)
                MuteOriginalAudio = true,
                TransitionCount = 2
            }
        };

        var result = await _service.ExportMultiTimelineProjectAsync(
            "BatchFromTemplate",
            items,
            targetDraftsRootDir: draftsRoot,
            templateFolderPath: templateDir);

        Assert.True(result.Success);
        Assert.Equal("MyTemplateProj", result.TemplateUsed);
        Assert.Equal(1, result.TimelinesCount);

        var timeline1Path = Path.Combine(result.ProjectDirectory, "draft_content.json");
        Assert.True(File.Exists(timeline1Path));

        var t1Json = JsonNode.Parse(await File.ReadAllTextAsync(timeline1Path))?.AsObject();
        Assert.NotNull(t1Json);

        // Verify duration is 20s (20,000,000 us)
        var finalDur = t1Json["duration"]?.GetValue<long>();
        Assert.Equal(20_000_000, finalDur);

        // Verify tracks count (should have 5 tracks from template: video, voice, bgm, text, sticker)
        var tracks = t1Json["tracks"]?.AsArray();
        Assert.NotNull(tracks);
        Assert.Equal(5, tracks.Count);

        // Verify text track exists and duration was scaled proportionally (8s * 2 = 16s)
        var textTrack = tracks.FirstOrDefault(t => t?["type"]?.ToString() == "text");
        Assert.NotNull(textTrack);
        var textSeg = textTrack?["segments"]?.AsArray()?.FirstOrDefault()?.AsObject();
        Assert.NotNull(textSeg);
        var textTargetDur = textSeg?["target_timerange"]?["duration"]?.GetValue<long>();
        Assert.Equal(16_000_000, textTargetDur);

        // Verify BGM track was preserved and scaled to 20s
        var bgmTrack = tracks.FirstOrDefault(t => t?["type"]?.ToString() == "audio" && t?["id"]?.ToString() != "track_voice_main");
        Assert.NotNull(bgmTrack);
        var bgmSeg = bgmTrack?["segments"]?.AsArray()?.FirstOrDefault()?.AsObject();
        Assert.NotNull(bgmSeg);
        var bgmTargetDur = bgmSeg?["target_timerange"]?["duration"]?.GetValue<long>();
        Assert.Equal(20_000_000, bgmTargetDur);

        // Cleanup
        if (Directory.Exists(draftsRoot))
        {
            Directory.Delete(draftsRoot, true);
        }
    }
}
