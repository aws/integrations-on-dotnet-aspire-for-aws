// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.CXAPI;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;

using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

public class CDKPublishingContext(IPublishingActivityProgressReporter activityReporter, ILambdaDeploymentPackager lambdaDeploymentPackager, ILogger logger)
{
    public async Task WriteModelAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        var publishingActivity = await activityReporter.CreateActivityAsync($"cdk-synth", $"Synthesizing CDK Application", false, cancellationToken);
        try
        {
            var outputPath = environment.CDKApp.Outdir;

            logger.LogInformation("Publishing to {output}", outputPath);
            ClearOutputDirectory(outputPath);

            ProcessElastiCacheRedisCluster(model, environment);

            await ProcessLambdaFunctionsAsync(model, environment, cancellationToken);
            await ProcessECSFargateServiceWithALBAsync(model, environment, cancellationToken);
            await ProcessECSFargateServiceAsync(model, environment, cancellationToken);

            var assembly = environment.CDKApp.Synth();

            await activityReporter.UpdateActivityStatusAsync(publishingActivity, (status) => status with { IsComplete = true }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to package synthesize CDK application");
            await activityReporter.UpdateActivityStatusAsync(
                publishingActivity,
                (status) => status with { IsError = true },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void ProcessElastiCacheRedisCluster(DistributedApplicationModel model, AWSCDKEnvironmentResource environment)
    {
        var resources = model.Resources.Where(r => r.HasAnnotationOfType<PublishCDKElasticCacheRedisAnnotation>()).ToArray();
        foreach(var resource in resources)
        {
            if (!resource.TryGetLastAnnotation<PublishCDKElasticCacheRedisAnnotation>(out var publishAnnotation))
            {
                throw new InvalidOperationException($"Missing {nameof(PublishCDKElasticCacheRedisAnnotation)} annotation");
            }

            var clusterProps = new CfnReplicationGroupProps
            {
                ReplicationGroupDescription = "Cache for Aspire Application",
                CacheNodeType = publishAnnotation.Config.CacheNodeType,
                Engine = publishAnnotation.Config.Engine.ToString().ToLowerInvariant(),
                EngineVersion = publishAnnotation.Config.EngineVersion,
                NumCacheClusters = 2,
                AutomaticFailoverEnabled = true,
                CacheSubnetGroupName = publishAnnotation.Config.CacheSubnetGroupName,
                SecurityGroupIds = publishAnnotation.Config.SecurityGroupIds,
                CacheParameterGroupName = publishAnnotation.Config.CacheParameterGroupName,
                Port = 6379
            };
            publishAnnotation.Config.PropsCfnReplicationGroupCallback?.Invoke(clusterProps);

            var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
            publishAnnotation.Config.ConstructCfnReplicationGroupCallback?.Invoke(cluster);
            resource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = cluster });
        }

    }

    private async Task ProcessLambdaFunctionsAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        if (File.Exists(tempFolder))
        {
            File.Delete(tempFolder);
        }

        var lambdaProjects = model.Resources.OfType<LambdaProjectResource>().Where(r => r.HasAnnotationOfType<PublishCDKLambdaAnnotation>()).ToArray();

        foreach (var lambdaFunction in lambdaProjects)
        {
            var publishingActivity = await activityReporter.CreateActivityAsync($"packaging-lambda-{lambdaFunction.Name}", $"Packaging Lambda function {lambdaFunction.Name}", false, cancellationToken);
            try
            {
                if (!lambdaFunction.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var lambdaFunctionAnnotation))
                {
                    throw new InvalidOperationException($"Missing {nameof(LambdaFunctionAnnotation)} annotation");
                }
                if (!lambdaFunction.TryGetLastAnnotation<PublishCDKLambdaAnnotation>(out var publishAnnotation))
                {
                    throw new InvalidOperationException($"Missing {nameof(PublishCDKLambdaAnnotation)} annotation");
                }

                logger.LogInformation("Creating deployment package for Lambda function '{LambdaFunctionName}'...", lambdaFunction.Name);
                var results = await lambdaDeploymentPackager.CreateDeploymentPackageAsync(lambdaFunction, tempFolder, cancellationToken);

                var functionProps = new FunctionProps
                {
                    Code = Code.FromAsset(results.LocalLocation!),
                    Handler = lambdaFunctionAnnotation!.Handler,
                    Runtime = Runtime.DOTNET_8, // TODO: Determine runtime based to TFM of project.
                    MemorySize = 256,
                };
                ProcessRelationShips(functionProps, lambdaFunction);
                publishAnnotation.Config.PropsFunctionCallback?.Invoke(functionProps);


                var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
                publishAnnotation.Config.ConstructFunctionCallback?.Invoke(function);
                lambdaFunction.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = function });

                await activityReporter.UpdateActivityStatusAsync(publishingActivity, (status) => status with { IsComplete = true }, cancellationToken);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Failed to package the Lambda function {LambdaFunctionName}.", lambdaFunction.Name);
                await activityReporter.UpdateActivityStatusAsync(
                    publishingActivity,
                    (status) => status with { IsError = true },
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessECSFargateServiceWithALBAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var ecsFargateWithALBProjects = model.Resources.OfType<ProjectResource>().Where(r => r.HasAnnotationOfType<PublishCDKECSFargateWithALBAnnotation>()).ToArray();
        foreach (var project in ecsFargateWithALBProjects)
        {
            if (!project.TryGetLastAnnotation<PublishCDKECSFargateWithALBAnnotation>(out var annotation))
            {
                throw new InvalidOperationException($"Missing {nameof(PublishCDKECSFargateWithALBAnnotation)} annotation");
            }

            var publishingActivity = await activityReporter.CreateActivityAsync($"ecs-{project.Name}", $"Packaging {project.Name} for ECS Fargate", false, cancellationToken);
            try
            {
                var projectAbsolutePath = Directory.GetParent(project.GetProjectMetadata().ProjectPath)!.FullName;
                var solutionFolder = DetermineSolutionFolder(projectAbsolutePath) ?? projectAbsolutePath;
                var relativeDockerPath = Path.GetRelativePath(solutionFolder!, Path.Combine(projectAbsolutePath, "Dockerfile"));

                var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
                {
                    Cluster = annotation.Config.ECSCluster,
                    Cpu = 256,
                    MemoryLimitMiB = 512,
                    DesiredCount = 2,
                    ListenerPort = 80,
                    PublicLoadBalancer = true,
                    MinHealthyPercent = 100,
                    TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                    {
                        // TODO: Figure out how to better handle docker build
                        // Image = ContainerImage.FromTarball(@"C:\codebase\integrations-on-dotnet-aspire-for-aws\frontend.tar"),
                        Image = ContainerImage.FromAsset(solutionFolder, new AssetImageProps
                        {
                            File = relativeDockerPath,
                        }),
                        ContainerPort = 8080,
                        Environment = new Dictionary<string, string>()
                    }
                };
                ProcessRelationShips(fargateServiceProps, project);
                annotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateServiceProps);

                var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{project.Name}", fargateServiceProps);
                annotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateService);
                project.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = fargateService });

                await activityReporter.UpdateActivityStatusAsync(publishingActivity, (status) => status with { IsComplete = true }, cancellationToken);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Failed to package {ProjectName} for ECS Fargate.", project.Name);
                await activityReporter.UpdateActivityStatusAsync(
                    publishingActivity,
                    (status) => status with { IsError = true },
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessECSFargateServiceAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var ecsFargateServiceProjects = model.Resources.OfType<ProjectResource>().Where(r => r.HasAnnotationOfType<PublishCDKECSFargateAnnotation>()).ToArray();
        foreach (var project in ecsFargateServiceProjects)
        {
            if (!project.TryGetLastAnnotation<PublishCDKECSFargateAnnotation>(out var annotation))
            {
                throw new InvalidOperationException($"Missing {nameof(PublishCDKECSFargateAnnotation)} annotation");
            }

            var publishingActivity = await activityReporter.CreateActivityAsync($"ecs-{project.Name}", $"Packaging {project.Name} for ECS Fargate", false, cancellationToken);
            try
            {
                var projectAbsolutePath = Directory.GetParent(project.GetProjectMetadata().ProjectPath)!.FullName;
                var solutionFolder = DetermineSolutionFolder(projectAbsolutePath) ?? projectAbsolutePath;
                var relativeDockerPath = Path.GetRelativePath(solutionFolder!, Path.Combine(projectAbsolutePath, "Dockerfile"));

                // Create Task Definition
                var fargateTaskDefinitionProps = new FargateTaskDefinitionProps
                {
                    MemoryLimitMiB = 512,
                    Cpu = 256
                };
                annotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(fargateTaskDefinitionProps);

                var taskDef = new FargateTaskDefinition(environment.CDKStack, $"TaskDefinition-{project.Name}", fargateTaskDefinitionProps);
                annotation.Config.ConstructFargateTaskDefinitionCallback?.Invoke(taskDef);

                // Create Container Definition
                var containerDefinitionProps = new ContainerDefinitionProps
                {
                    // TODO: Figure out how to better handle docker build
                    Image = ContainerImage.FromAsset(solutionFolder, new AssetImageProps
                    {
                        File = relativeDockerPath,
                    }),
                    Environment = new Dictionary<string, string>(),
                    Logging = new AwsLogDriver(new AwsLogDriverProps
                    {
                        StreamPrefix = environment.CDKStack.StackName
                    })
                };
                ApplyRelationshipEnvironmentVariable(containerDefinitionProps.Environment, project);
                annotation.Config.PropsContainerDefinitionCallback?.Invoke(containerDefinitionProps);

                var containerDefinition = taskDef.AddContainer($"Container-{project.Name}", containerDefinitionProps);
                annotation.Config.ConstructContainerDefinitionCallback?.Invoke(containerDefinition);

                // Create Fargate Service
                var fargateServiceProps = new FargateServiceProps
                {
                    Cluster = annotation.Config.ECSCluster,
                    DesiredCount = annotation.Config.DesiredCount,
                    MinHealthyPercent = annotation.Config.MinHealthyPercent,
                    TaskDefinition = taskDef,
                };
                annotation.Config.PropsFargateServiceCallback?.Invoke(fargateServiceProps);

                var fargateService = new FargateService(environment.CDKStack, $"Project-{project.Name}", fargateServiceProps);
                annotation.Config.ConstructFargateServiceCallback?.Invoke(fargateService);
                project.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = fargateService });

                await activityReporter.UpdateActivityStatusAsync(publishingActivity, (status) => status with { IsComplete = true }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to package {ProjectName} for ECS Fargate.", project.Name);
                await activityReporter.UpdateActivityStatusAsync(
                    publishingActivity,
                    (status) => status with { IsError = true },
                    cancellationToken).ConfigureAwait(false);
            }
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
}
