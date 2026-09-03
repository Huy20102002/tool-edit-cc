using System.Diagnostics;
using AutoVideoEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.FFmpeg;

public class FFmpegLocator : IFFmpegLocator
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<FFmpegLocator>? _logger;
    private string? _cachedFFmpegPath;
    private string? _cachedFFprobePath;
    private string? _cachedVersion;

    public FFmpegLocator(ISettingsService settingsService, ILogger<FFmpegLocator>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public string GetFFmpegPath()
    {
        if (!string.IsNullOrEmpty(_cachedFFmpegPath) && File.Exists(_cachedFFmpegPath))
            return _cachedFFmpegPath;

        var customPath = _settingsService.CurrentSettings?.CustomFFmpegPath;
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            _cachedFFmpegPath = customPath;
            return _cachedFFmpegPath;
        }

        var found = FindExecutable("ffmpeg.exe");
        _cachedFFmpegPath = found ?? "ffmpeg";
        return _cachedFFmpegPath;
    }

    public string GetFFprobePath()
    {
        if (!string.IsNullOrEmpty(_cachedFFprobePath) && File.Exists(_cachedFFprobePath))
            return _cachedFFprobePath;

        var customPath = _settingsService.CurrentSettings?.CustomFFprobePath;
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            _cachedFFprobePath = customPath;
            return _cachedFFprobePath;
        }

        var found = FindExecutable("ffprobe.exe");
        _cachedFFprobePath = found ?? "ffprobe";
        return _cachedFFprobePath;
    }

    public bool IsFFmpegAvailable()
    {
        try
        {
            var path = GetFFmpegPath();
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public bool IsFFprobeAvailable()
    {
        try
        {
            var path = GetFFprobePath();
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetFFmpegVersion()
    {
        if (!string.IsNullOrEmpty(_cachedVersion))
            return _cachedVersion;

        try
        {
            var path = GetFFmpegPath();
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadLine();
                proc.WaitForExit(2000);
                if (!string.IsNullOrEmpty(output))
                {
                    _cachedVersion = output;
                    return output;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get FFmpeg version");
        }

        return "Không xác định";
    }

    private static string? FindExecutable(string exeName)
    {
        // 1. Check application directory & subdirectories
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, exeName),
            Path.Combine(baseDir, "ffmpeg", exeName),
            Path.Combine(baseDir, "ffmpeg", "bin", exeName),
            Path.Combine(baseDir, "bin", exeName),
            Path.Combine(baseDir, "tools", exeName),
            Path.Combine(baseDir, "tools", "ffmpeg", "bin", exeName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // 2. Search PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in paths)
            {
                try
                {
                    var fullPath = Path.Combine(p.Trim(), exeName);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch
                {
                    // Ignore invalid path in env
                }
            }
        }

        // Default fallback to exeName (let OS try PATH)
        return null;
    }
}
