// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using static Google.Protobuf.Reflection.GeneratedCodeInfo.Types;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Lambda lifecycle hook takes care of getting Amazon.Lambda.TestTool installed if there was
/// a Lambda service emulator added to the resources.
/// </summary>
/// <param name="logger"></param>
internal class LambdaLifecycleHook(ILogger<LambdaEmulatorResource> logger, IProcessCommandService processCommandService) : IDistributedApplicationLifecycleHook
{
    internal const string DefaultLambdaTestToolVersion = "0.0.2-preview";

    public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        LambdaEmulatorAnnotation? emulatorAnnotation = null;
        if (appModel.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out emulatorAnnotation)) != null && emulatorAnnotation != null)
        {
            await ApplyLambdaEmulatorAnnotationAsync(emulatorAnnotation, cancellationToken);
        }
        else
        {
            logger.LogDebug("Skipping installing Amazon.Lambda.TestTool since no Lambda emulator resource was found");
        }
    }

    internal async Task ApplyLambdaEmulatorAnnotationAsync(LambdaEmulatorAnnotation emulatorAnnotation, CancellationToken cancellationToken = default)
    {
        if (emulatorAnnotation.DisableAutoInstall)
        {
            return;
        }

        var expectedVersion = emulatorAnnotation.OverrideMinimumInstallVersion ?? DefaultLambdaTestToolVersion;
        var installedVersion = await GetCurrentInstalledVersionAsync(cancellationToken);

        if (ShouldInstall(installedVersion, expectedVersion, emulatorAnnotation.AllowDowngrade))
        {
            logger.LogDebug("Installing .NET Tool Amazon.Lambda.TestTool ({version})", installedVersion);

            var commandLineArgument = $"tool install -g Amazon.Lambda.TestTool --version {expectedVersion}";
            if (emulatorAnnotation.AllowDowngrade)
            {
                commandLineArgument += " --allow-downgrade";
            }

            var result = await processCommandService.RunProcessAndCaptureOuputAsync(logger, "dotnet", commandLineArgument, cancellationToken);
            if (result.ExitCode == 0)
            {
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    logger.LogInformation("Successfully Updated .NET Tool Amazon.Lambda.TestTool from version {installedVersion} to {newVersion}", installedVersion, expectedVersion);
                }
                else
                {
                    logger.LogInformation("Successfully installed .NET Tool Amazon.Lambda.TestTool ({version})", expectedVersion);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    logger.LogWarning("Failed to update Amazon.Lambda.TestTool from {installedVersion} to {expectedVersion}:\n{output}", installedVersion, expectedVersion, result.Output);
                }
                else
                {
                    logger.LogError("Fail to install Amazon.Lambda.TestTool ({version}) required for running Lambda functions locally:\n{output}", expectedVersion, result.Output);
                }
            }
        }
        else
        {
            logger.LogInformation("Amazon.Lambda.TestTool version {version} already installed", installedVersion);
        }
    }

    internal static bool ShouldInstall(string currentInstalledVersion, string expectedVersionStr, bool allowDowngrading)
    {
        if (string.IsNullOrEmpty(currentInstalledVersion))
        {
            return true;
        }

        var installedVersion = Version.Parse(currentInstalledVersion.Replace("-preview", string.Empty));
        var expectedVersion = Version.Parse(expectedVersionStr.Replace("-preview", string.Empty));

        return (installedVersion < expectedVersion) || (allowDowngrading && installedVersion != expectedVersion);
    }

    private async Task<string> GetCurrentInstalledVersionAsync(CancellationToken cancellationToken)
    {
        var results = await processCommandService.RunProcessAndCaptureOuputAsync(logger, "dotnet", "lambda-test-tool --tool-info", cancellationToken);
        if (results.ExitCode != 0)
        {
            return string.Empty;
        }

        try
        {
            var versionDoc = JsonNode.Parse(results.Output);
            if (versionDoc == null)
            {
                logger.LogWarning("Error parsing version information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
                return string.Empty;

            }
            var version = versionDoc["version"]?.ToString();
            logger.LogDebug("Installed version of Amazon.Lambda.TestTool is {version}", version);
            return version ?? string.Empty;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error parsing version information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
            return string.Empty;
        }
    }
}
