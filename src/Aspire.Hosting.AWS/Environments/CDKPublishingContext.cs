// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKPublishingContext(IPublishingActivityReporter activityReporter, ILambdaDeploymentPackager lambdaDeploymentPackager, ITarballContainerImageBuilder imageBuilder, ILogger<CDKPublishingContext> logger)
{
    public async Task WriteModelAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        var step = await activityReporter.CreateStepAsync($"Synthesizing CDK Application", cancellationToken);
        try
        {
            var outputPath = environment.CDKApp.Outdir;

            logger.LogInformation("Publishing to {output}", outputPath);
            ClearOutputDirectory(outputPath);

            ProcessElastiCacheRedisCluster(model, environment);

            foreach(var project in model.Resources.OfType<ProjectResource>())
            {
                if (project.IsExcludedFromPublish())
                    continue;

                IResourceAnnotation? annotation = null;
                if (project.TryGetLastAnnotation<PublishCDKECSFargateWithALBAnnotation>(out var fargateWebAnnotation))
                {
                    annotation = fargateWebAnnotation;
                }
                else if (project.TryGetLastAnnotation<PublishCDKECSFargateAnnotation>(out var fargateServiceAnnotation))
                {
                    annotation = fargateServiceAnnotation;
                }
                else if (project.TryGetLastAnnotation<PublishCDKLambdaAnnotation>(out var lambdaFunctionAnnotation))
                {
                    annotation = lambdaFunctionAnnotation;
                }
                
                if (annotation == null)
                {
                    annotation = DetermineDefaultPublishAnnotation(environment, project);
                }

                if (annotation == null)
                    continue;

                await ProcessPublishAnnotation(step, model, environment, project, annotation!, cancellationToken);
            }

            var assembly = environment.CDKApp.Synth();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FixCDKAssetsFileForWindows(outputPath);
            }

            await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to package synthesize CDK application");
            await step.FailAsync($"Failed to package synthesize CDK application: {ex}", cancellationToken);
        }
    }

    private IResourceAnnotation? DetermineDefaultPublishAnnotation(AWSCDKEnvironmentResource environment, ProjectResource projectResource)
    {
        if (projectResource is LambdaProjectResource)
        {
            var annotation = new PublishCDKLambdaAnnotation();
            return annotation;
        }
        if (environment.DefaultComputeService == DeploymentComputeService.ECSFargate)
        {
            if(projectResource.GetEndpoints().Any())
            {
                var annotation = new PublishCDKECSFargateWithALBAnnotation();
                return annotation;
            }
            else
            {
                var annotation = new PublishCDKECSFargateAnnotation();
                return annotation;
            }
        }

        return null;
    }

    private async Task ProcessPublishAnnotation(IPublishingStep step, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, IResourceAnnotation annotation, CancellationToken cancellationToken)
    {
        switch(annotation)
        {
            case PublishCDKLambdaAnnotation lambdaAnnotation:
                if (!(projectResource is LambdaProjectResource))
                {
                    throw new InvalidOperationException($"Project resource {projectResource.Name} is not a {nameof(LambdaProjectResource)}");
                }
                await ProcessLambdaFunctionAsync(step, model, environment, (LambdaProjectResource)projectResource, lambdaAnnotation, cancellationToken);
                break;
            case PublishCDKECSFargateWithALBAnnotation fargateWithALBAnnotation:
                await ProcessECSFargateServiceWithALBAsync(step, model, environment, projectResource, fargateWithALBAnnotation, cancellationToken);
                break;
            case PublishCDKECSFargateAnnotation fargateAnnotation:
                await ProcessECSFargateServiceAsync(step, model, environment, projectResource, fargateAnnotation, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported publish annotation type: {annotation.GetType().FullName}");
        }
    }

    private void ProcessElastiCacheRedisCluster(DistributedApplicationModel model, AWSCDKEnvironmentResource environment)
    {
        var resources = model.Resources.Where(r => r is RedisResource).ToArray();
        foreach(var resource in resources)
        {
            if (resource.IsExcludedFromPublish())
                continue;

            if (!resource.TryGetLastAnnotation<PublishCDKElasticCacheRedisAnnotation>(out var publishAnnotation))
            {
                publishAnnotation = new PublishCDKElasticCacheRedisAnnotation();
            }

            var clusterProps = new CfnReplicationGroupProps();
            publishAnnotation.Config.PropsCfnReplicationGroupCallback?.Invoke(clusterProps);
            environment.DefaultValuesProvider.ApplyCfnReplicationGroupPropsDefaults(environment, clusterProps);

            var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
            publishAnnotation.Config.ConstructCfnReplicationGroupCallback?.Invoke(cluster);
            resource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = cluster });
        }
    }

    private async Task ProcessLambdaFunctionAsync(IPublishingStep step, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, LambdaProjectResource lambdaFunction, PublishCDKLambdaAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        if (File.Exists(tempFolder))
        {
            File.Delete(tempFolder);
        }

        var activityTask = await step.CreateTaskAsync($"Packaging Lambda function {lambdaFunction.Name}", cancellationToken);
        try
        {
            if (!lambdaFunction.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var lambdaFunctionAnnotation))
            {
                throw new InvalidOperationException($"Missing {nameof(LambdaFunctionAnnotation)} annotation");
            }

            logger.LogInformation("Creating deployment package for Lambda function '{LambdaFunctionName}'...", lambdaFunction.Name);
            var results = await lambdaDeploymentPackager.CreateDeploymentPackageAsync(lambdaFunction, tempFolder, cancellationToken);

            var functionProps = new FunctionProps
            {
                Code = Code.FromAsset(results.LocalLocation!),
                Handler = lambdaFunctionAnnotation!.Handler
            };
            ProcessRelationShips(functionProps, lambdaFunction);
            publishAnnotation.Config.PropsFunctionCallback?.Invoke(functionProps);
            environment.DefaultValuesProvider.ApplyLambdaFunctionDefaults(lambdaFunction.GetProjectMetadata().ProjectPath, functionProps);

            var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
            publishAnnotation.Config.ConstructFunctionCallback?.Invoke(function);
            lambdaFunction.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = function });

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to package the Lambda function {LambdaFunctionName}.", lambdaFunction.Name);
            await activityTask.FailAsync($"Failed to package the Lambda function {lambdaFunction.Name}.: { ex}", cancellationToken);
            throw;
        }
    }


    private async Task ProcessECSFargateServiceWithALBAsync(IPublishingStep step, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, PublishCDKECSFargateWithALBAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await step.CreateTaskAsync($"Packaging {projectResource.Name} for ECS Fargate", cancellationToken);
        try
        {
            var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

            var taskImageOptions = new ApplicationLoadBalancedTaskImageOptions
            {
                Image = ContainerImage.FromTarball(imageTarballPath),
                Environment = new Dictionary<string, string>()
            };

            publishAnnotation.Config.PropsApplicationLoadBalancedTaskImageOptionsCallback?.Invoke(taskImageOptions);
            environment.DefaultValuesProvider.ApplyECSFargateServiceWithALBDefaults(taskImageOptions);

            var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
            {
                TaskImageOptions = taskImageOptions
            };
            ProcessRelationShips(fargateServiceProps, projectResource);
            publishAnnotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceWithALBDefaults(environment, fargateServiceProps);

            var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateService);
            projectResource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = fargateService });

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to package {ProjectName} for ECS Fargate.", projectResource.Name);
            await activityTask.FailAsync($"Failed to package {projectResource.Name} for ECS Fargate: { ex}", cancellationToken);
            throw;
        }
    }

    private async Task ProcessECSFargateServiceAsync(IPublishingStep step, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, PublishCDKECSFargateAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await step.CreateTaskAsync($"Packaging {projectResource.Name} for ECS Fargate", cancellationToken);
        try
        {
            var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

            // Create Task Definition
            var fargateTaskDefinitionProps = new FargateTaskDefinitionProps();
            publishAnnotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(fargateTaskDefinitionProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(fargateTaskDefinitionProps);

            var taskDef = new FargateTaskDefinition(environment.CDKStack, $"TaskDefinition-{projectResource.Name}", fargateTaskDefinitionProps);
            publishAnnotation.Config.ConstructFargateTaskDefinitionCallback?.Invoke(taskDef);

            // Create Container Definition
            var containerDefinitionProps = new ContainerDefinitionProps
            {
                Image = ContainerImage.FromTarball(imageTarballPath),
                Environment = new Dictionary<string, string>()
            };
            ApplyRelationshipEnvironmentVariable(containerDefinitionProps.Environment, projectResource);
            publishAnnotation.Config.PropsContainerDefinitionCallback?.Invoke(containerDefinitionProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(environment, projectResource.Name, containerDefinitionProps);

            var containerDefinition = taskDef.AddContainer($"Container-{projectResource.Name}", containerDefinitionProps);
            publishAnnotation.Config.ConstructContainerDefinitionCallback?.Invoke(containerDefinition);

            // Create Fargate Service
            var fargateServiceProps = new FargateServiceProps
            {
                TaskDefinition = taskDef,
            };
            publishAnnotation.Config.PropsFargateServiceCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceDefaults(environment, fargateServiceProps);

            var fargateService = new FargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructFargateServiceCallback?.Invoke(fargateService);
            projectResource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = fargateService });

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to package {ProjectName} for ECS Fargate.", projectResource.Name);
            await activityTask.FailAsync($"Failed to package {projectResource.Name} for ECS Fargate: { ex}", cancellationToken);
            throw;
        }
    }

    private void ProcessRelationShips(ApplicationLoadBalancedFargateServiceProps props, IResource resource)
    {
        if (props.TaskImageOptions?.Environment == null)
        {
            throw new InvalidOperationException("TaskImageOptions.Environment must be set for the ApplicationLoadBalancedFargateServiceProps");
        }
        ApplyRelationshipEnvironmentVariable(props.TaskImageOptions.Environment, resource);
    }

    private void ProcessRelationShips(FunctionProps props, IResource resource)
    {
        if (props.Environment == null)
        {
            props.Environment = new Dictionary<string, string>();
        }

        ApplyRelationshipEnvironmentVariable(props.Environment, resource);
    }

    private void ApplyRelationshipEnvironmentVariable(IDictionary<string, string> environmentVariables, IResource resource)
    {
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>().Where(annotation => annotation.Resource is IResourceWithConnectionString);
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (!relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotations>(out var targetLinkedConstructAnnotation))
                continue;

            if (targetLinkedConstructAnnotation.LinkedConstruct is CfnReplicationGroup construct)
            {
                environmentVariables[$"ConnectionStrings__{relatedAnnotation.Resource.Name}"] = $"{Token.AsString(construct.AttrPrimaryEndPointAddress)}:{Token.AsString(construct.AttrPrimaryEndPointPort)}";
            }
        }
    }

    private string? DetermineSolutionFolder(string projectFolder)
    {
        if (Directory.GetFiles(projectFolder, "*.sln").Any())
        {
            return projectFolder;
        }

        var parent = Directory.GetParent(projectFolder)?.FullName;
        if (parent == null)
            return null;

        return DetermineSolutionFolder(parent);
    }

    private void ClearOutputDirectory(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            logger.LogTrace("Clearing output directory '{outputPath}'...", outputPath);
            foreach (var file in Directory.EnumerateFiles(outputPath))
            {
                logger.LogTrace("Deleting file '{file}'...", file);
                File.Delete(file);
            }
            foreach (var directory in Directory.EnumerateDirectories(outputPath))
            {
                logger.LogTrace("Deleting directory '{directory}'...", directory);
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void FixCDKAssetsFileForWindows(string outputPath)
    {
        foreach (var file in Directory.EnumerateFiles(outputPath, "*.assets.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);
                JsonNode root = JsonNode.Parse(json)!;
                bool changed = false;

                if (root["dockerImages"] is JsonObject dockerImages)
                {
                    foreach (var image in dockerImages)
                    {
                        JsonObject imageObject = image.Value!.AsObject();

                        if (imageObject["source"]?["executable"] is JsonArray)
                        {
                            // Replace the executable array
                            imageObject["source"]!["executable"] = new JsonArray("powershell", "-Command", $"docker load -i asset.{image.Key}.tar | ForEach-Object {{ ($_ -replace '^Loaded image: ', '') }}");
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    var updatedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(file, updatedJson);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error switching CDK assets file {file} to use PowerShell for restoring tarball", file);
            }
        }
    }
}
