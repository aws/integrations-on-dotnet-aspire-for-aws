// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Pipelines;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKPublishingContext(ITarballContainerImageBuilder imageBuilder, ILogger<CDKPublishingContext> logger)
{
    public async Task WriteModelAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Synthesizing CDK Application", cancellationToken);
        try
        {
            var outputPath = environment.CDKApp.Outdir;

            logger.LogInformation("Publishing to {output}", outputPath);
            ClearOutputDirectory(outputPath);

            ProcessElastiCacheRedisCluster(context, model, environment);
            await ProcessProjects(context, step, model, environment, cancellationToken);

            var assembly = environment.CDKApp.Synth();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FixCDKAssetsFileForWindows(outputPath);
            }

            await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synthesize CDK application");
            await step.FailAsync($"Failed to synthesize CDK application: {ex}", cancellationToken);
        }
    }

    private async Task ProcessProjects(PipelineStepContext context, IReportingTask step, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        foreach (var projectResource in model.Resources.OfType<ProjectResource>())
        {
            if (projectResource.IsExcludedFromPublish())
                continue;

            IResourceAnnotation? annotation = null;
            if (projectResource.TryGetLastAnnotation<PublishCDKECSFargateWithALBAnnotation>(out var fargateWebAnnotation))
            {
                annotation = fargateWebAnnotation;
            }
            if (projectResource.TryGetLastAnnotation<PublishCDKECSFargateExpressAnnotation>(out var fargateExpressAnnotation))
            {
                annotation = fargateExpressAnnotation;
            }
            else if (projectResource.TryGetLastAnnotation<PublishCDKECSFargateAnnotation>(out var fargateServiceAnnotation))
            {
                annotation = fargateServiceAnnotation;
            }
            else if (projectResource.TryGetLastAnnotation<PublishCDKLambdaAnnotation>(out var lambdaFunctionAnnotation))
            {
                annotation = lambdaFunctionAnnotation;
            }

            if (annotation == null)
            {
                annotation = DetermineDefaultPublishAnnotation(environment, projectResource);
            }

            if (annotation == null)
                continue;

            switch (annotation)
            {
                case PublishCDKLambdaAnnotation lambdaAnnotation:
                    if (!(projectResource is LambdaProjectResource))
                    {
                        throw new InvalidOperationException($"Project resource {projectResource.Name} is not a {nameof(LambdaProjectResource)}");
                    }
                    await ProcessLambdaFunctionAsync(context, model, environment, (LambdaProjectResource)projectResource, lambdaAnnotation, cancellationToken);
                    break;
                case PublishCDKECSFargateExpressAnnotation:
                    await ProcessECSFargateExpressServiceAsync(context, model, environment, projectResource, (PublishCDKECSFargateExpressAnnotation)fargateExpressAnnotation!, cancellationToken);
                    break;
                case PublishCDKECSFargateWithALBAnnotation fargateWithALBAnnotation:
                    await ProcessECSFargateServiceWithALBAsync(context, model, environment, projectResource, fargateWithALBAnnotation, cancellationToken);
                    break;
                case PublishCDKECSFargateAnnotation fargateAnnotation:
                    await ProcessECSFargateServiceAsync(context, model, environment, projectResource, fargateAnnotation, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported publish annotation type: {annotation.GetType().FullName}");
            }
        }
    }

    private IResourceAnnotation? DetermineDefaultPublishAnnotation(AWSCDKEnvironmentResource environment, ProjectResource projectResource)
    {
        if (projectResource is LambdaProjectResource)
        {
            var annotation = new PublishCDKLambdaAnnotation();
            return annotation;
        }
        if (environment.PreferredComputeService == DeploymentComputeService.ECSFargate)
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

    private void ProcessElastiCacheRedisCluster(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment)
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
            ApplyLinkedConstructAnnotation(resource, cluster);
        }
    }

    private async Task ProcessLambdaFunctionAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, LambdaProjectResource lambdaFunction, PublishCDKLambdaAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await context.ReportingStep.CreateTaskAsync($"Preparing Lambda function {lambdaFunction.Name}", cancellationToken);
        try
        {
            if (!lambdaFunction.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var lambdaFunctionAnnotation))
            {
                throw new InvalidOperationException($"Missing {nameof(LambdaFunctionAnnotation)} annotation");
            }

            var functionProps = new FunctionProps
            {
                Code = Code.FromAsset(lambdaFunctionAnnotation.DeploymentBundlePath!),
                Handler = lambdaFunctionAnnotation.Handler
            };
            ProcessRelationShips(functionProps, lambdaFunction);
            publishAnnotation.Config.PropsFunctionCallback?.Invoke(functionProps);
            environment.DefaultValuesProvider.ApplyLambdaFunctionDefaults(lambdaFunction.GetProjectMetadata().ProjectPath, functionProps);

            var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
            publishAnnotation.Config.ConstructFunctionCallback?.Invoke(function);
            ApplyLinkedConstructAnnotation(lambdaFunction, function);

            await ApplyDeploymentTagAsync(environment, lambdaFunction, function, cancellationToken);

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to package the Lambda function {LambdaFunctionName}.", lambdaFunction.Name);
            await activityTask.FailAsync($"Failed to package the Lambda function {lambdaFunction.Name}.: { ex}", cancellationToken);
            throw;
        }
    }

    private async Task ProcessECSFargateExpressServiceAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, PublishCDKECSFargateExpressAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await context.ReportingStep.CreateTaskAsync($"Preparing {projectResource.Name} for ECS Fargate", cancellationToken);
        try
        {
            var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

            var asset = new TarballImageAsset(environment.CDKStack, $"ContainerTarBall-{projectResource.Name}", new TarballImageAssetProps
            {
                TarballFile = imageTarballPath
            });

            var primaryContainer = new ExpressGatewayContainerProperty
            {
                Image = asset.ImageUri
            };

            var fargateServiceProps = new CfnExpressGatewayServiceProps
            {
                PrimaryContainer = primaryContainer,
                ExecutionRoleArn = "arn:aws:iam::626492997873:role/ecsTaskExecutionRole",
                InfrastructureRoleArn = "arn:aws:iam::626492997873:role/ecsInfrastructureRoleForExpressServices",
                ServiceName = projectResource.Name
            };
            publishAnnotation.Config.PropsCfnExpressGatewayServicePropsCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyCfnExpressGatewayServiceDefaults(environment, fargateServiceProps);
            ProcessRelationShips(fargateServiceProps, projectResource);

            var fargateService = new CfnExpressGatewayService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructCfnExpressGatewayServiceCallback?.Invoke(fargateService);
            ApplyLinkedConstructAnnotation(projectResource, fargateService);
            
            _ = new CfnOutput(environment.CDKStack, "ExpressGatewayEndpoint", new CfnOutputProps
            {
                Description = "Endpoint for the ECS Express Gateway Service",
                Value = Fn.Join("", ["https://", Fn.GetAtt(fargateService.LogicalId, "Endpoint").ToString(), "/"]) 
            });            

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare {ProjectName} for ECS Fargate.", projectResource.Name);
            await activityTask.FailAsync($"Failed to prepare {projectResource.Name} for ECS Fargate: {ex}", cancellationToken);
            throw;
        }
    }

    private async Task ProcessECSFargateServiceWithALBAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, PublishCDKECSFargateWithALBAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await context.ReportingStep.CreateTaskAsync($"Preparing {projectResource.Name} for ECS Fargate", cancellationToken);
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
            publishAnnotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateServiceProps);
            environment.DefaultValuesProvider.ApplyECSFargateServiceWithALBDefaults(environment, fargateServiceProps);
            ProcessRelationShips(fargateServiceProps, projectResource);

            var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
            publishAnnotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateService);
            ApplyLinkedConstructAnnotation(projectResource, fargateService);

            await ApplyDeploymentTagAsync(environment, projectResource, fargateService.Service, cancellationToken);

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to prepare {ProjectName} for ECS Fargate.", projectResource.Name);
            await activityTask.FailAsync($"Failed to prepare {projectResource.Name} for ECS Fargate: { ex}", cancellationToken);
            throw;
        }
    }

    private async Task ProcessECSFargateServiceAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, ProjectResource projectResource, PublishCDKECSFargateAnnotation publishAnnotation, CancellationToken cancellationToken)
    {
        var activityTask = await context.ReportingStep.CreateTaskAsync($"Preparing {projectResource.Name} for ECS Fargate", cancellationToken);
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
            ApplyLinkedConstructAnnotation(projectResource, fargateService);

            await ApplyDeploymentTagAsync(environment, projectResource, fargateService, cancellationToken);

            await activityTask.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare {ProjectName} for ECS Fargate.", projectResource.Name);
            await activityTask.FailAsync($"Failed to prepare {projectResource.Name} for ECS Fargate: { ex}", cancellationToken);
            throw;
        }
    }

    private void ProcessRelationShips(CfnExpressGatewayServiceProps props, IResource resource)
    {
        var dict = new Dictionary<string, string>();
        ApplyRelationshipEnvironmentVariable(dict, resource);

        if (!dict.Any())
            return;

        var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
        if (primaryContainer == null)
            throw new InvalidDataException("PrimaryContainer must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayContainerProperty");

        var kvp = new List<IKeyValuePairProperty>();
        
        var existingKvp = primaryContainer.Environment as IKeyValuePairProperty[];
        if (existingKvp != null)
        {
            kvp.AddRange(existingKvp);
        }

        foreach (var item in dict)
        {
            kvp.Add(new KeyValuePairProperty
            {
                Name = item.Key,
                Value = item.Value
            });
        }

        primaryContainer.Environment = kvp.ToArray();
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
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotations>(out var targetLinkedConstructAnnotation))
                continue;

            if (targetLinkedConstructAnnotation.LinkedConstruct is CfnReplicationGroup cacheConstruct)
            {
                environmentVariables[$"ConnectionStrings__{relatedAnnotation.Resource.Name}"] = $"{Token.AsString(cacheConstruct.AttrPrimaryEndPointAddress)}:{Token.AsString(cacheConstruct.AttrPrimaryEndPointPort)}";
            }
            else if (targetLinkedConstructAnnotation.LinkedConstruct is ApplicationLoadBalancedFargateService albFargateConstruct)
            {
                foreach(var listener in albFargateConstruct.LoadBalancer.Listeners)
                {
                    string protocol = listener.Port == 443 ? "https" : "http";
                    environmentVariables[$"services__{relatedAnnotation.Resource.Name}__{protocol}__0"] = $"{protocol}://{Token.AsString(albFargateConstruct.LoadBalancer.LoadBalancerDnsName)}:{Token.AsString(listener.Port)}/";
                }
            }
        }
    }

    private void ApplyLinkedConstructAnnotation(IResource resource, Construct sourceConstruct)
    {
        resource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = sourceConstruct });

        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotations>(out var targetLinkedConstructAnnotation))
                continue;

            sourceConstruct.Node.AddDependency(targetLinkedConstructAnnotation.LinkedConstruct);
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

    private async Task ApplyDeploymentTagAsync(AWSCDKEnvironmentResource environment, IResource aspireResource, IConstruct scope, CancellationToken cancellationToken)
    {
        if (aspireResource.TryGetLastAnnotation<DeploymentImageTagCallbackAnnotation>(out var deploymentTag))
        {
            var context = new DeploymentImageTagCallbackAnnotationContext
            {
                Resource = aspireResource,
                CancellationToken = cancellationToken,
            };
            var tag = await deploymentTag.Callback(context).ConfigureAwait(false);
            if (tag != null)
            {
                Tags.Of(scope).Add(environment.DefaultValuesProvider.DeploymentTagName, tag);
            }
        }
    }
}
