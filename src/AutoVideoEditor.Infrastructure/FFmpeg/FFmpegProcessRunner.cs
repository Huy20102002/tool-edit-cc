using System.Diagnostics;
using AutoVideoEditor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoVideoEditor.Infrastructure.FFmpeg;

public class FFmpegProcessRunner : IFFmpegProcessRunner
{
    private readonly ILogger<FFmpegProcessRunner>? _logger;

    public FFmpegProcessRunner(ILogger<FFmpegProcessRunner>? logger = null)
    {
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(
        string executablePath,
        string arguments,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executing: {Exe} {Args}", executablePath, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {executablePath}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while starting process {Exe}", executablePath);
            throw;
        }

        // Close stdin so FFmpeg never waits for terminal input
        try
        {
            process.StandardInput.Close();
        }
        catch
        {
            // Ignore
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger?.LogWarning("Canceling process PID {Pid}...", process.Id);
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error while killing process PID {Pid}", process.Id);
            }
        });

        var stdOutTask = Task.Run(async () =>
        {
            using var reader = process.StandardOutput;
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                onStdOut?.Invoke(line);
            }
        });

        var stdErrTask = Task.Run(async () =>
        {
            using var reader = process.StandardError;
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                onStdErr?.Invoke(line);
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill errors during cancellation
            }
            throw;
        }

        // Wait for output streams to finish reading
        await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);

        return process.ExitCode;
    }
}
