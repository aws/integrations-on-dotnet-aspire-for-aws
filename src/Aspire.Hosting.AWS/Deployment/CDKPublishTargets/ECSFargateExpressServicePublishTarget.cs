// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateExpressServicePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateExpressServicePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate Express Service";

    public override Type PublishTargetAnnotation => typeof(PublishECSFargateServiceExpressAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishECSFargateServiceExpressAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishECSFargateServiceExpressAnnotation)}.");

        var imageTarballPath = await imageBuilder.CreateTarballImageAsync(projectResource, cancellationToken);

        // Create the task definition that describes the container(s) run by the Express service. The
        // Express service points at this task definition via its TaskDefinitionArn property (rather than
        // the inline PrimaryContainer property), giving parity with the other ECS Fargate publish targets.
        var fargateTaskDefinitionProps = new FargateTaskDefinitionProps();
        publishAnnotation.Config.PropsFargateTaskDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), fargateTaskDefinitionProps);
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceTaskDefinitionDefaults(fargateTaskDefinitionProps);

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

        // Create the container definition. Environment variables from Aspire references are wired onto the
        // container, and the shared cluster security group / default task role are exposed to the reference
        // hooks via the connection points.
        var containerDefinitionProps = new ContainerDefinitionProps
        {
            ContainerName = "Main", // The application container is required to be named "Main" for ECS Fargate Express Service.
            Image = ContainerImage.FromTarball(imageTarballPath),
            Environment = new Dictionary<string, string>()
        };
        ProcessRelationShips(new ExpressServiceContainerConnectionPoints(
                containerDefinitionProps,
                environment.DefaultsProvider.GetDefaultECSClusterSecurityGroup(),
                defaultTaskRole),
            projectResource, environment);

        publishAnnotation.Config.PropsContainerDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), containerDefinitionProps);

        if (containerDefinitionProps.ContainerName != "Main")
            throw new InvalidOperationException("ECS Fargate Express requires the application container to be named \"Main\".");

        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceContainerDefinitionDefaults(projectResource.Name, containerDefinitionProps);

        var containerDefinition = taskDef.AddContainer($"Container-{projectResource.Name}", containerDefinitionProps);
        publishAnnotation.Config.ConstructContainerDefinitionCallback?.Invoke(CreatePublishTargetContext(environment), containerDefinition);

        // Create the Express Gateway service pointing at the task definition.
        var fargateServiceProps = new CfnExpressGatewayServiceProps
        {
            TaskDefinitionArn = taskDef.TaskDefinitionArn,
            ServiceName = projectResource.Name
        };
        publishAnnotation.Config.PropsCfnExpressGatewayServicePropsCallback?.Invoke(CreatePublishTargetContext(environment), fargateServiceProps);
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(fargateServiceProps);

        var fargateService = new CfnExpressGatewayService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructCfnExpressGatewayServiceCallback?.Invoke(CreatePublishTargetContext(environment), fargateService);
        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, fargateService, this);

        _ = new CfnOutput(environment.CDKStack, $"{resource.Name}-ExpressGatewayEndpoint", new CfnOutputProps
        {
            Description = "Endpoint for the ECS Express Gateway Service",
            Value = Fn.Join("", ["https://", Fn.GetAtt(fargateService.LogicalId, "Endpoint").ToString(), "/"])
        });
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is ProjectResource projectResource &&
            projectResource.GetEndpoints().Any() &&
            cdkDefaultsProvider.DefaultWebProjectResourcePublishTarget == CDKDefaultsProvider.WebProjectResourcePublishTarget.ECSFargateExpressService
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishECSFargateServiceExpressAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100 // Override to raise rank over console application default
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not CfnExpressGatewayService fargateExpressConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        var key = $"services__{linkedAnnotation.Resource.Name}__https__0";
        var endpoint = Fn.Join("", ["https://", Fn.GetAtt(fargateExpressConstruct.LogicalId, "Endpoint").ToString(), "/"]);
        result.EnvironmentVariables[key] = endpoint;

        return result;
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ExpressServiceContainerConnectionPoints(ContainerDefinitionProps props, ISecurityGroup securityGroup, IRole? taskRole = null) : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.Environment ?? new Dictionary<string, string>();
        set => props.Environment = value ?? new Dictionary<string, string>();
    }

    public override ISecurityGroup? ReferenceSecurityGroup => securityGroup;

    public override IRole? ReferenceTaskRole => taskRole;
}

