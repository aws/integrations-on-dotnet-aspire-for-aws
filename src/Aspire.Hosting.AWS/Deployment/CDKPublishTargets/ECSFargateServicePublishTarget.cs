// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate";

    public override Type PublishTargetAnnotation => typeof(PublishECSFargateServiceAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishECSFargateServiceAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishECSFargateServiceAnnotation)}.");

        var imageTarballPath = await imageBuilder.CreateTarballImageAsync(projectResource, cancellationToken);

        // Create Task Definition
        var fargateTaskDefinitionProps = new FargateTaskDefinitionProps();
        publishAnnotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), fargateTaskDefinitionProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(fargateTaskDefinitionProps);

        // Assign a default task role when the user hasn't supplied one so the application's own AWS calls
        // run under a controllable role. Only the integration-created role is exposed to reference hooks
        // (via ReferenceTaskRole) so a user-supplied role is never mutated.
        IRole? defaultTaskRole = null;
        if (fargateTaskDefinitionProps.TaskRole == null)
        {
            defaultTaskRole = environment.DefaultsProvider.CreateDefaultECSTaskRole(projectResource.Name);
            fargateTaskDefinitionProps.TaskRole = defaultTaskRole;
        }

        var taskDef = new FargateTaskDefinition(environment.CDKStack, $"TaskDefinition-{projectResource.Name}", fargateTaskDefinitionProps);
        publishAnnotation.Config.ConstructFargateTaskDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), taskDef);

        // Create Container Definition
        var containerDefinitionProps = new ContainerDefinitionProps
        {
            Image = ContainerImage.FromTarball(imageTarballPath),
            Environment = new Dictionary<string, string>()
        };
        ProcessRelationShips(new ContainerDefinitionPropsConnectionPoints(containerDefinitionProps), projectResource, environment);

        publishAnnotation.Config.PropsContainerDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), containerDefinitionProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(projectResource.Name, containerDefinitionProps);

        var containerDefinition = taskDef.AddContainer($"Container-{projectResource.Name}", containerDefinitionProps);
        publishAnnotation.Config.ConstructContainerDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), containerDefinition);

        // Create Fargate Service
        var fargateServiceProps = new FargateServiceProps
        {
            TaskDefinition = taskDef,
        };
        publishAnnotation.Config.PropsFargateServiceCallback?.Invoke(CreatePublishTargetContext(environment), fargateServiceProps);
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(fargateServiceProps);
        ProcessRelationShips(new FargateServicePropsConnectionPoints(
            () => CreateEmptyReferenceSecurityGroup(environment, projectResource, fargateServiceProps, x => x.SecurityGroups, (x, v) => x.SecurityGroups = v),
            defaultTaskRole),
            resource, environment);

        var fargateService = new FargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructFargateServiceCallback?.Invoke(CreatePublishTargetContext(environment), fargateService);
        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, fargateService, this);
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

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        return new ReferenceConnectionInfo();
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ContainerDefinitionPropsConnectionPoints(ContainerDefinitionProps props) : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.Environment ?? new Dictionary<string, string>();
        set => props.Environment = value ?? new Dictionary<string, string>();
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class FargateServicePropsConnectionPoints(Func<ISecurityGroup> securityGroupFactory, IRole? taskRole = null) : AbstractCDKConstructConnectionPoints
{
    ISecurityGroup? _referenceSecurityGroup;

    public override ISecurityGroup? ReferenceSecurityGroup
    {
        get
        {
            _referenceSecurityGroup ??= securityGroupFactory();
            return _referenceSecurityGroup;
        }
    }

    public override IRole? ReferenceTaskRole => taskRole;
}
