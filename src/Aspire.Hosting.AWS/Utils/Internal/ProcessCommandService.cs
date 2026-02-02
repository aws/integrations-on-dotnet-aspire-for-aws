// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aspire.Hosting.AWS.Utils.Internal;

/// <summary>
/// An internal service interface for shelling out commands
/// </summary>
public interface IProcessCommandService
{
    /// <summary>
    /// Record capturing the exit code and console output. The Output will be the combined stdout and stderr.
    /// </summary>
    /// <param name="ExitCode"></param>
    /// <param name="Output"></param>
    public record RunProcessAndCaptureStdOutResult(int ExitCode, string Output);

    /// <summary>
    /// Method to shell out commands.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="workingDirectory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOutputAsync(ILogger logger, string path, string arguments, string? workingDirectory, CancellationToken cancellationToken);


    /// <summary>
    /// Method to shell out commands.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="workingDirectory"></param>
    /// <returns>Exit code</returns>
    int RunProcess(ILogger logger, string path, string arguments, string workingDirectory, bool streamOutputToLogger, IDictionary<string, string>? environmentVariables = null);

    IProcessCommandService.RunProcessAndCaptureStdOutResult RunCDKProcess(ILogger? logger, LogLevel logLevel, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null);
}

internal class ProcessCommandService : IProcessCommandService
{

    /// <summary>
    /// Utility method for running a command on the commandline. It returns backs the exit code and anything written to stdout or stderr.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="workingDirectory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IProcessCommandService.RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOutputAsync(ILogger? logger, string path, string arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory == null ? Directory.GetCurrentDirectory() : workingDirectory,
                FileName = path,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var queue = new ConcurrentQueue<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                queue.Enqueue(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                queue.Enqueue(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            // If this fails then it most likely means the executable being invoked does not exist.
            logger?.LogDebug(ex, "Failed to start process {process}.", path);
            return new IProcessCommandService.RunProcessAndCaptureStdOutResult(-404, string.Empty);
        }

        await process.WaitForExitAsync(cancellationToken);

        var output = new StringBuilder();

        while (!queue.IsEmpty)
        {
            while (queue.TryDequeue(out var data))
            {
                output.Append(data);
            }
        }

        if (process.ExitCode != 0)
        {
            logger?.LogDebug("Process {process} exited with code {exitCode}.", path, process.ExitCode);
            return new IProcessCommandService.RunProcessAndCaptureStdOutResult(process.ExitCode, output.ToString());
        }

        return new IProcessCommandService.RunProcessAndCaptureStdOutResult(process.ExitCode, output.ToString());

    }
    
    /// <summary>
    /// Utility method for running a command on the commandline. It returns backs the exit code.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="workingDirectory"></param>
    /// <returns></returns>
    public int RunProcess(ILogger logger, string path, string arguments, string workingDirectory, bool streamOutputToLogger, IDictionary<string, string>? environmentVariables)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = path,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        var output = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                if (streamOutputToLogger)
                    logger.LogInformation(e.Data);
                output.Append(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                if (streamOutputToLogger)
                    logger.LogInformation(e.Data);
                output.Append(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(int.MaxValue);

        logger.LogDebug(output.ToString());
        
        if (process.ExitCode != 0)
        {
            logger.LogDebug("Process {process} exited with code {exitCode}.", path, process.ExitCode);
        }
        
        return process.ExitCode;
    }

    public IProcessCommandService.RunProcessAndCaptureStdOutResult RunCDKProcess(ILogger? logger, LogLevel logLevel, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null)
    {
        string shellCommand;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shellCommand = "powershell";
            arguments = $"-NoProfile -Command \"cdk \"{arguments}\"";
        }
        else
        {
            shellCommand = "sh";
            arguments = $"-c \"cdk {arguments}\"";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = shellCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        var output = new StringBuilder();
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                logger?.Log(logLevel, e.Data);
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                logger?.Log(logLevel, e.Data);
                output.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(int.MaxValue);

        if (process.ExitCode != 0)
        {
            logger?.LogDebug("Process exited with code {exitCode}.", process.ExitCode);
        }

        return new IProcessCommandService.RunProcessAndCaptureStdOutResult(process.ExitCode, output.ToString());
    }
}
