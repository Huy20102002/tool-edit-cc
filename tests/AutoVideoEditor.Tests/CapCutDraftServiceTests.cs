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
}
