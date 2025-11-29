// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Environments;

public interface ILambdaDeploymentPackager
{
    Task<LambdaDeploymentPackagerOutput> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken);
}

internal class LambdaDeploymentPackager(IProcessCommandService processCommandService, ILogger<LambdaDeploymentPackager> logger) : ILambdaDeploymentPackager
{
    public async Task<LambdaDeploymentPackagerOutput> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken)
    {
        processCommandService.RunProcess(logger, "dotnet", "tool install --global Amazon.Lambda.Tools", Environment.CurrentDirectory);

        var zipFilePath = Path.Combine(outputDirectory,  $"{lambdaFunction.Name}.zip");
        var results = await processCommandService.RunProcessAndCaptureOuputAsync(
                logger, 
                "dotnet", 
                $"lambda package --output \"{zipFilePath}\"", 
                Directory.GetParent(lambdaFunction.GetProjectMetadata().ProjectPath)!.FullName, 
                cancellationToken);

        var logLevel = results.ExitCode == 0 ? LogLevel.Debug : LogLevel.Error;
        logger.Log(logLevel, "Package output: {output}", results.Output);

        return await Task.FromResult(new LambdaDeploymentPackagerOutput { Success = results.ExitCode == 0, LocalLocation = results.ExitCode == 0 ? zipFilePath : null });
    }
}

public class LambdaDeploymentPackagerOutput
{
    public required bool Success { get; init; }
    public string? LocalLocation { get; init; }
}