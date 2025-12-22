// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using static Aspire.Hosting.AWS.Environments.CDKResourceContexts.IAWSPublishTarget;

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate";

    public override Type PublishTargetAnnotation => typeof(PublishCDKECSFargateAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, ApplicationModel.IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
            ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishCDKECSFargateAnnotation
            ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKECSFargateAnnotation)}.");

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
        ApplyLinkedConstructAnnotation(projectResource, fargateService, this);

        await ApplyDeploymentTagAsync(environment, projectResource, fargateService, cancellationToken);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
    {
        if (resource is ProjectResource &&
            defaultProvider.DefaultConsoleAppPublishTarget == DefaultProvider.ConsoleAppPublishTaret.ECSFargateService
            )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishCDKECSFargateAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
    {
        return null;
    }
}
