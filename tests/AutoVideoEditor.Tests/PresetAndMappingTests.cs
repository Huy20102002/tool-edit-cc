using AutoVideoEditor.Core.Enums;
using AutoVideoEditor.Core.Models;
using Xunit;

namespace AutoVideoEditor.Tests;

public class PresetAndMappingTests
{
    [Fact]
    public void GetDefaultPresets_ContainsStandardPlatforms()
    {
        var presets = ExportPreset.GetDefaultPresets();

        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name.Contains("TikTok") && p.ResolutionWidth == 1080 && p.ResolutionHeight == 1920);
        Assert.Contains(presets, p => p.Name.Contains("YouTube Shorts"));
        Assert.Contains(presets, p => p.Name.Contains("YouTube Standard") && p.ResolutionWidth == 1920 && p.ResolutionHeight == 1080);
    }

    [Fact]
    public void Preset_Clone_CreatesDistinctInstanceWithNewId()
    {
        var original = ExportPreset.GetDefaultPresets()[0];
        var cloned = original.Clone("Custom Clone Preset");

        Assert.NotEqual(original.Id, cloned.Id);
        Assert.Equal("Custom Clone Preset", cloned.Name);
        Assert.False(cloned.IsBuiltIn);
        Assert.Equal(original.ResolutionWidth, cloned.ResolutionWidth);
        Assert.Equal(original.ResolutionHeight, cloned.ResolutionHeight);
    }
}
