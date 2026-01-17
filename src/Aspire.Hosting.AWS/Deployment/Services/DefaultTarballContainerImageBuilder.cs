// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Deployment.Services;

internal class DefaultTarballContainerImageBuilder(ILogger<DefaultTarballContainerImageBuilder> logger, IProcessCommandService processCommandService) : ITarballContainerImageBuilder
{
    public async Task<string> CreateTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken)
    {
        var tarballFilePath = Path.GetTempFileName() + ".tar";

        var imageTag = resource.Name.ToLower() + ":latest";
        var dockerSaveCommand = $"docker save -o {tarballFilePath} {imageTag}";
        string shellCommand;
        string arguments;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shellCommand = "cmd";
            arguments = $"/c \"{dockerSaveCommand}\"";
        }
        else
        {
            shellCommand = "sh";
            arguments = $"-c \"{dockerSaveCommand}\"";
        }

        var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, shellCommand, arguments, Environment.CurrentDirectory, cancellationToken);
        if (results.ExitCode != 0)
        {
            logger.LogError("Failed to save container image {ImageTag} as tarball for publish assets. Exit Code: {ExitCode}, Output: {Output}", imageTag, results.ExitCode, results.Output);
            throw new InvalidOperationException($"Failed to save container image {resource.Name} as tarball for publish assets.");
        }
        

        return tarballFilePath;
    }
}
