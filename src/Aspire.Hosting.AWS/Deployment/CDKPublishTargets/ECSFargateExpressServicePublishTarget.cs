// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;
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
            ServiceName = projectResource.Name
        };
        publishAnnotation.Config.PropsCfnExpressGatewayServicePropsCallback?.Invoke(CreatePublishTargetContext(environment), fargateServiceProps);
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(fargateServiceProps);

        var referencePoints = new CfnExpressGatewayServicePropsConnectionPoints(
            fargateServiceProps,
            environment.DefaultsProvider.GetDefaultECSClusterSecurityGroup());
        ProcessRelationShips(referencePoints, projectResource);

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
internal class CfnExpressGatewayServicePropsConnectionPoints(CfnExpressGatewayServiceProps props, ISecurityGroup securityGroup) : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get
        {
            var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
            if (primaryContainer == null)
                throw new InvalidDataException("PrimaryContainer must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayContainerProperty");

            var existingKvp = primaryContainer.Environment as IKeyValuePairProperty[] ?? [];

            var environmentVariables = new Dictionary<string, string>();
            foreach (var kvp in existingKvp)
            {
                environmentVariables[kvp.Name] = kvp.Value;
            }

            return environmentVariables;
        }
        set
        {
            var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
            if (primaryContainer == null)
                throw new InvalidDataException("PrimaryContainer must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayContainerProperty");

            var list = new List<IKeyValuePairProperty>();
            if (value != null)
            {
                foreach (var kvp in value)
                {
                    list.Add(new KeyValuePairProperty { Name = kvp.Key, Value = kvp.Value });
                }
            }

            primaryContainer.Environment = list.ToArray();
        }
    }

    public override ISecurityGroup? ReferenceSecurityGroup => securityGroup;
}

