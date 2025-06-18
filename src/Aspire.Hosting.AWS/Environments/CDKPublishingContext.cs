// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.CXAPI;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Logging;

using Construct = Constructs.Construct;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

public class CDKPublishingContext(string outputPath, ILambdaDeploymentPackager lambdaDeploymentPackager, ILogger logger)
{
    public async Task WriteModelAsync(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        if (new DirectoryInfo(outputPath).Name != "cdk.out")
        {
            outputPath = Path.Combine(outputPath, "cdk.out");
        }

        logger.LogInformation("Publishing to {output}", outputPath);
        ClearOutputDirectory();

        ProcessElastiCacheRedisCluster(model, environment, cancellationToken);

        await ProcessLambdaFunctionsAsync(model, environment, cancellationToken);
        ProcessECSFargateWithALB(model, environment, cancellationToken);

        var assembly = environment.CDKApp.Synth();

        // TODO: Make sure Dockerfiles are copied over to the asset folder even if they are in the .dockeringore file
        MoveTempContentToOutput(assembly);
    }

    private void ProcessElastiCacheRedisCluster(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
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
            publishAnnotation.Config.PropsCallback?.Invoke(clusterProps);

            var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
            publishAnnotation.Config.ConstructCallback?.Invoke(cluster);
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
            publishAnnotation.Config.PropsCallback?.Invoke(functionProps);


            var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
            publishAnnotation.Config.ConstructCallback?.Invoke(function);
            lambdaFunction.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = function });
        }
    }

    private void ProcessECSFargateWithALB(DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var ecsFargateWithALBProjects = model.Resources.OfType<ProjectResource>().Where(r => r.HasAnnotationOfType<PublishCDKECSFargateWithALBAnnotation>()).ToArray();
        foreach (var project in ecsFargateWithALBProjects)
        {
            if (!project.TryGetLastAnnotation<PublishCDKECSFargateWithALBAnnotation>(out var annotation))
            {
                throw new InvalidOperationException($"Missing {nameof(PublishCDKECSFargateWithALBAnnotation)} annotation");
            }

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
                    Image = ContainerImage.FromAsset(solutionFolder, new AssetImageProps
                    {
                        File = relativeDockerPath,
                    }),
                    ContainerPort = 8080,
                    Environment = new Dictionary<string, string>()
                }
            };
            ProcessRelationShips(fargateServiceProps, project);
            annotation.Config.PropsCallback?.Invoke(fargateServiceProps);

            var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{project.Name}", fargateServiceProps);
            annotation.Config.ConstructCallback?.Invoke(fargateService);
            project.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = fargateService });
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

    private void ClearOutputDirectory()
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

    private void MoveTempContentToOutput(CloudAssembly cloudAssembly)
    {
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
        foreach(var stack in cloudAssembly.Stacks)
        {
            Directory.Move(Directory.GetParent(stack.TemplateFullPath)!.FullName, Path.Combine(outputPath, stack.StackName));
        }
    }
}
