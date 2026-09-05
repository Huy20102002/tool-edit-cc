using AutoVideoEditor.AudioEngine;
using AutoVideoEditor.Core.Interfaces;
using AutoVideoEditor.Infrastructure.FFmpeg;
using AutoVideoEditor.Infrastructure.Hardware;
using AutoVideoEditor.Infrastructure.Services;
using AutoVideoEditor.VideoEngine;
using Microsoft.Extensions.DependencyInjection;

namespace AutoVideoEditor.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAutoVideoEditorServices(this IServiceCollection services)
    {
        // Infrastructure Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ITempFileManager, TempFileManager>();
        services.AddSingleton<IFFmpegLocator, FFmpegLocator>();
        services.AddSingleton<IFFmpegProcessRunner, FFmpegProcessRunner>();
        services.AddSingleton<IFFprobeService, FFprobeService>();
        services.AddSingleton<IHardwareDetector, HardwareDetector>();
        services.AddSingleton<ICapCutDraftService, CapCut.CapCutDraftService>();

        // Audio Engine
        services.AddSingleton<ISilenceDetector, SilenceDetector>();
        services.AddSingleton<IWaveformGenerator, WaveformGenerator>();
        services.AddSingleton<IAudioAnalyzer, AudioAnalyzer>();

        // Video Engine
        services.AddSingleton<ISceneDetector, SceneDetector>();
        services.AddSingleton<IVideoAnalyzer, VideoAnalyzer>();
        services.AddSingleton<ITransitionPlanner, TransitionPlanner>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        services.AddSingleton<IVideoRenderer, VideoRenderer>();

        // Job Orchestration
        services.AddSingleton<IJobManager, JobManager>();

        return services;
    }
}
