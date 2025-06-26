// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

public interface ITarballContainerImageBuilder
{
    Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default(CancellationToken));
}

internal class DefaultTarballContainerImageBuilder(ILogger<DefaultTarballContainerImageBuilder> logger, IResourceContainerImageBuilder containerImageBuilder, IProcessCommandService processCommandService) : ITarballContainerImageBuilder
{
    public async Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken)
    {
        await containerImageBuilder.BuildImageAsync(resource, cancellationToken);
        var tarballFilePath = Path.GetTempFileName() + ".tar";

        var imageTag = resource.Name.ToLower() + ":latest";
        var dockerSaveCommand = $"docker save -o {tarballFilePath} {imageTag}";
        string shellCommand;
        string arguments;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shellCommand = "powershell";
            arguments = $"-Command \"{dockerSaveCommand}\"";
        }
        else
        {
            shellCommand = "sh";
            arguments = $"-c \"{dockerSaveCommand}\"";
        }

        if (processCommandService.RunProcess(logger, shellCommand, arguments, Environment.CurrentDirectory) != 0)
        {
            throw new InvalidOperationException($"Failed to save container image {resource.Name} as tarball for publish assets.");
        }
        

        return tarballFilePath;
    }
}