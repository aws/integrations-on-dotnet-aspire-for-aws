// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Deployment.Services;

internal class DefaultStaticSiteBuilder(IProcessCommandService processCommandService, ILogger<DefaultStaticSiteBuilder> logger) : IStaticSiteBuilder
{
    public Task BuildAsync(IResource resource, string workingDirectory, IDictionary<string, string> environmentVariables, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executableName = "npm";
        var scriptCommand = "run";
        if (resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pkgManager))
        {
            executableName = pkgManager.ExecutableName;
            scriptCommand = pkgManager.ScriptCommand;
        }

        var buildScript = "build";
        var buildArgs = Array.Empty<string>();
        if (resource.TryGetLastAnnotation<JavaScriptBuildScriptAnnotation>(out var buildAnnotation))
        {
            buildScript = buildAnnotation.ScriptName;
            buildArgs = buildAnnotation.Args ?? [];
        }

        var arguments = buildArgs.Length > 0
            ? $"{scriptCommand} {buildScript} -- {string.Join(" ", buildArgs)}"
            : $"{scriptCommand} {buildScript}";

        logger.LogInformation("Building static site for '{ResourceName}': {Executable} {Arguments}", resource.Name, executableName, arguments);

        var exitCode = processCommandService.RunProcess(logger, executableName, arguments, workingDirectory, streamOutputToLogger: true, environmentVariables);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Static site build for '{resource.Name}' failed with exit code {exitCode}. " +
                $"Command: {executableName} {arguments} in '{workingDirectory}'");
        }

        return Task.CompletedTask;
    }
}
