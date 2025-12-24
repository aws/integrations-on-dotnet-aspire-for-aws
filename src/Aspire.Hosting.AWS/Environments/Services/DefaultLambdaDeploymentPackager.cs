using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Environments.Services;

internal class DefaultLambdaDeploymentPackager(IProcessCommandService processCommandService, ILogger<DefaultLambdaDeploymentPackager> logger) : ILambdaDeploymentPackager
{
    public async Task<LambdaDeploymentPackagerOutput> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken)
    {
        processCommandService.RunProcess(logger, "dotnet", "tool install --global Amazon.Lambda.Tools", Environment.CurrentDirectory, streamOutputToLogger: false);

        var zipFilePath = Path.Combine(outputDirectory,  $"{lambdaFunction.Name}.zip");
        var exitCode = processCommandService.RunProcess(
            logger, 
            "dotnet", 
            $"lambda package --output \"{zipFilePath}\"", 
            Directory.GetParent(lambdaFunction.GetProjectMetadata().ProjectPath)!.FullName, 
            streamOutputToLogger: true);

        return await Task.FromResult(new LambdaDeploymentPackagerOutput { Success = exitCode == 0, LocalLocation = exitCode == 0 ? zipFilePath : null });
    }
}