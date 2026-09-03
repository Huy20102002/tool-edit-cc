using AutoVideoEditor.Core.Models;

namespace AutoVideoEditor.Core.Interfaces;

public interface IFFmpegLocator
{
    string GetFFmpegPath();
    string GetFFprobePath();
    bool IsFFmpegAvailable();
    bool IsFFprobeAvailable();
    string GetFFmpegVersion();
}

public interface IFFmpegProcessRunner
{
    Task<int> ExecuteAsync(
        string executablePath,
        string arguments,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        CancellationToken cancellationToken = default);
}

public interface IFFprobeService
{
    Task<MediaFileInfo> ProbeFileAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IHardwareDetector
{
    Task<HardwareCapabilities> DetectCapabilitiesAsync(CancellationToken cancellationToken = default);
}
