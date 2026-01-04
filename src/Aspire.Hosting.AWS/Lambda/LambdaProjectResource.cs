// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Lambda;
#pragma warning disable ASPIREPIPELINES001
/// <summary>
/// Aspire resource representing a Lambda function.
/// </summary>
public class LambdaProjectResource : ProjectResource
{
    /// <summary>
    /// Creates an instance of LambdaProjectResource.
    /// </summary>
    /// <param name="name">The name of the Aspire Resource</param>
    public LambdaProjectResource(string name)
        : base(name)
    {
        // Remove the default PipelineStepAnnotation added by ProjectResource which will trigger a container build not compatible
        // with the Lambda project.
        var addedPipelineAnnotations = Annotations.Where(a => a.GetType() == typeof(PipelineStepAnnotation)).ToList();
        foreach (var annotation in addedPipelineAnnotations)
        {
            Annotations.Remove(annotation);
        }

        Annotations.Add(new PipelineStepAnnotation((factoryContext) =>
        {
            if (factoryContext.Resource.IsExcludedFromPublish())
            {
                return [];
            }

            var buildStep = new PipelineStep
            {
                Name = $"build-{name}",
                Action = BuildLambdaDeploymentBundle,
                Tags = [WellKnownPipelineTags.BuildCompute],
                RequiredBySteps = [WellKnownPipelineSteps.Build],
                DependsOnSteps = [WellKnownPipelineSteps.BuildPrereq]
            };

            return [buildStep];
        }));

    }

    private async Task BuildLambdaDeploymentBundle(PipelineStepContext ctx)
    {
        var lambdaDeploymentPackager = ctx.Services.GetRequiredService<ILambdaDeploymentPackager>();
        var logger = ctx.Logger;

        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        if (File.Exists(tempFolder))
        {
            File.Delete(tempFolder);
        }

        if(!this.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var lambdaFunctionAnnotation))
        {
            return;
        }

        logger.LogInformation("Creating deployment package for Lambda function '{LambdaFunctionName}'...", this.Name);
        var results = await lambdaDeploymentPackager.CreateDeploymentPackageAsync(this, tempFolder, ctx.CancellationToken);

        if (!results.Success)
        {
            throw new DistributedApplicationException($"Failed to lambda deployment bundle for resource {this.Name}.");
        }

        lambdaFunctionAnnotation.DeploymentBundlePath = results.LocalLocation;
    }
}
