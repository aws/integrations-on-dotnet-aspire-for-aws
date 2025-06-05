// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Environments;

internal class CDKInfrastructureLifecycleHook(
    ILogger<CDKInfrastructureLifecycleHook> logger,
    ILambdaDeploymentPackager lambdaDeploymentPackager,
    DistributedApplicationExecutionContext executionContext) : IDistributedApplicationLifecycleHook
{
    public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (executionContext.IsRunMode)
        {
            return;
        }

        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "aws.out");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var lambdaFunctions = appModel.Resources.OfType<LambdaProjectResource>().ToArray();
        foreach (var lambdaFunction in lambdaFunctions)
        {
            logger.LogInformation("Creating deployment package for Lambda function '{LambdaFunctionName}'...", lambdaFunction.Name);
            await lambdaDeploymentPackager.CreateDeploymentPackageAsync(lambdaFunction, outputDirectory, cancellationToken);
        }
    }
}
