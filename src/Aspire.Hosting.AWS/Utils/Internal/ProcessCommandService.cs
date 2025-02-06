﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOuputAsync(ILogger logger, string path, string arguments, CancellationToken cancellationToken);
}

internal class ProcessCommandService : IProcessCommandService
{

    /// <summary>
    /// Utility method for running a command on the commandline. It returns backs the exit code and anything written to stdout or stderr.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IProcessCommandService.RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOuputAsync(ILogger logger, string path, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                FileName = path,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var output = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.Append(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.Append(e.Data);
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
            // If this fails then it most likey means the executable being invoked does not exist.
            logger.LogDebug(ex, "Failed to start process {process}.", path);
            return new IProcessCommandService.RunProcessAndCaptureStdOutResult(-404, string.Empty);
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            logger.LogDebug("Process {process} exited with code {exitCode}.", path, process.ExitCode);
            return new IProcessCommandService.RunProcessAndCaptureStdOutResult(process.ExitCode, output.ToString());
        }

        return new IProcessCommandService.RunProcessAndCaptureStdOutResult(process.ExitCode, output.ToString());

    }
}
