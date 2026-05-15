// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIRECONTAINERRUNTIME001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Deployment.Services;

internal class DefaultTarballContainerImageBuilder(ILogger<DefaultTarballContainerImageBuilder> logger, IProcessCommandService processCommandService, IContainerRuntime containerRuntime) : ITarballContainerImageBuilder
{
    public async Task<string> CreateTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken)
    {
        var tarballFilePath = Path.GetTempFileName() + ".tar";

        var imageTag = resource.Name.ToLower() + ":latest";
        var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, containerRuntime.Name, $"save -o {tarballFilePath} {imageTag}", Environment.CurrentDirectory, cancellationToken);
        if (results.ExitCode != 0)
        {
            logger.LogError("Failed to save container image {ImageTag} as tarball for publish assets. Exit Code: {ExitCode}, Output: {Output}", imageTag, results.ExitCode, results.Output);
            throw new InvalidOperationException($"Failed to save container image {resource.Name} as tarball for publish assets.");
        }

        return tarballFilePath;
    }
}
