// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
using Aspire.Hosting.AWS.Environments.Services;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate";

    public override Type PublishTargetAnnotation => typeof(PublishECSFargateServiceAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, ApplicationModel.IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishECSFargateServiceAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishECSFargateServiceAnnotation)}.");

        var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

        // Create Task Definition
        var fargateTaskDefinitionProps = new FargateTaskDefinitionProps();
        publishAnnotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(fargateTaskDefinitionProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(fargateTaskDefinitionProps);

        var taskDef = new FargateTaskDefinition(environment.CDKStack, $"TaskDefinition-{projectResource.Name}", fargateTaskDefinitionProps);
        publishAnnotation.Config.ConstructFargateTaskDefinitionCallback?.Invoke(taskDef);

        // Create Container Definition
        var containerDefinitionProps = new ContainerDefinitionProps
        {
            Image = ContainerImage.FromTarball(imageTarballPath),
            Environment = new Dictionary<string, string>()
        };
        ProcessRelationShipsEnvironmentVariables(containerDefinitionProps, projectResource);
        publishAnnotation.Config.PropsContainerDefinitionCallback?.Invoke(containerDefinitionProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(projectResource.Name, containerDefinitionProps);

        var containerDefinition = taskDef.AddContainer($"Container-{projectResource.Name}", containerDefinitionProps);
        publishAnnotation.Config.ConstructContainerDefinitionCallback?.Invoke(containerDefinition);

        // Create Fargate Service
        var fargateServiceProps = new FargateServiceProps
        {
            TaskDefinition = taskDef,
        };
        publishAnnotation.Config.PropsFargateServiceCallback?.Invoke(fargateServiceProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(fargateServiceProps);
        ProcessRelationShipsSecurityGroups(environment, fargateServiceProps, projectResource);

        var fargateService = new FargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructFargateServiceCallback?.Invoke(fargateService);
        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, fargateService, this);

        await ApplyDeploymentTagAsync(environment, projectResource, fargateService, cancellationToken);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is ProjectResource &&
            cdkDefaultsProvider.DefaultConsoleProjectResourcePublishTarget == CDKDefaultsProvider.ConsoleProjectResourcePublishTarget.ECSFargateService
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishECSFargateServiceAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override GetReferencesResult GetAllReferences(IResource resource, IConstruct resourceConstruct)
    {
        return new GetReferencesResult();
    }

    private void ProcessRelationShipsEnvironmentVariables(ContainerDefinitionProps props, ApplicationModel.IResource resource)
    {
        var environmentVariables = props.Environment ?? new Dictionary<string, string>();
        var allReferences = GetAllReferences(resource);
        foreach (var reference in allReferences)
        {
            if (reference.EnvironmentVariables != null)
            {
                foreach (var kvp in reference.EnvironmentVariables)
                {
                    environmentVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        props.Environment = environmentVariables;
    }
    
    private void ProcessRelationShipsSecurityGroups(AWSCDKEnvironmentResource environmentResource, FargateServiceProps props, ApplicationModel.IResource resource)
    {
        HashSet<string> securityGroupIds = new HashSet<string>();
        if (props.SecurityGroups != null)
        {
            foreach (var securityGroup in props.SecurityGroups)
            {
                securityGroupIds.Add(securityGroup.SecurityGroupId);
            }
        }

        var allReferences = GetAllReferences(resource);
        foreach (var reference in allReferences)
        {
            if (reference.SecurityGroupsIds != null)
            {
                foreach (var securityGroupId in reference.SecurityGroupsIds)
                {
                    securityGroupIds.Add(securityGroupId);
                }
            }
        }

        var securityGroups = new List<ISecurityGroup>();
        var securityGroupsIdsList = securityGroupIds.ToList();
        for (var i = 0; i < securityGroupsIdsList.Count; i++)
        {
            var securityGroup =
                SecurityGroup.FromSecurityGroupId(environmentResource.CDKStack, $"Reference-{resource.Name}-{i}", securityGroupsIdsList[i]);
            securityGroups.Add(securityGroup);
        }
        props.SecurityGroups = securityGroups.ToArray();
    }    
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateServiceConfig
{
    public Action<ContainerDefinitionProps>? PropsContainerDefinitionCallback { get; set; }

    public Action<ContainerDefinition>? ConstructContainerDefinitionCallback { get; set; }

    public Action<FargateTaskDefinitionProps>? PropsFargateTaskDefinitionCallback { get; set; }

    public Action<FargateTaskDefinition>? ConstructFargateTaskDefinitionCallback { get; set; }

    public Action<FargateServiceProps>? PropsFargateServiceCallback { get; set; }

    public Action<FargateService>? ConstructFargateServiceCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishECSFargateServiceAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateServiceConfig Config { get; init; } = new PublishECSFargateServiceConfig();
}