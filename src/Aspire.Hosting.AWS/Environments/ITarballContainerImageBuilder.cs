// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREPIPELINES003

public interface ITarballContainerImageBuilder
{
    Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default(CancellationToken));
}

internal class DefaultTarballContainerImageBuilder(ILogger<DefaultTarballContainerImageBuilder> logger, IProcessCommandService processCommandService) : ITarballContainerImageBuilder
{
    public async Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken)
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

        var results = await processCommandService.RunProcessAndCaptureOuputAsync(logger, shellCommand, arguments, Environment.CurrentDirectory, cancellationToken);
        if (results.ExitCode != 0)
        {
            logger.LogError("Failed to save container image {ImageTag} as tarball for publish assets. Exit Code: {ExitCode}, Output: {Output}", imageTag, results.ExitCode, results.Output);
            throw new InvalidOperationException($"Failed to save container image {resource.Name} as tarball for publish assets.");
        }
        

        return tarballFilePath;
    }
}