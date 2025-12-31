// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001
using Amazon.CDK;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
using Aspire.Hosting.AWS.Environments.Services;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

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
            ServiceName = projectResource.Name
        };
        publishAnnotation.Config.PropsCfnExpressGatewayServicePropsCallback?.Invoke(fargateServiceProps);
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(fargateServiceProps);
        ProcessRelationShips(environment, fargateServiceProps, projectResource);

        var fargateService = new CfnExpressGatewayService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructCfnExpressGatewayServiceCallback?.Invoke(fargateService);
        ApplyLinkedConstructAnnotation(environment, projectResource, fargateService, this);

        _ = new CfnOutput(environment.CDKStack, "ExpressGatewayEndpoint", new CfnOutputProps
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

    public override GetReferencesResult GetAllReferences(IResource resource, IConstruct resourceConstruct)
    {
        var result = new GetReferencesResult();
        if (resourceConstruct is not CfnExpressGatewayService fargateExpressConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        var key = $"services__{resource.Name}__https__0";
        var endpoint = Fn.Join("", ["https://", Fn.GetAtt(fargateExpressConstruct.LogicalId, "Endpoint").ToString(), "/"]);
        result.EnvironmentVariables[key] = endpoint;

        return result;
    }

    private void ProcessRelationShips(AWSCDKEnvironmentResource environmentResource, CfnExpressGatewayServiceProps props, ApplicationModel.IResource resource)
    {
        var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
        if (primaryContainer == null)
            throw new InvalidDataException("PrimaryContainer must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayContainerProperty");

        ExpressGatewayServiceNetworkConfigurationProperty? networkConfiguration;
        if (props.NetworkConfiguration != null)
        {
            networkConfiguration = props.NetworkConfiguration as ExpressGatewayServiceNetworkConfigurationProperty;
            if (networkConfiguration == null)
                throw new InvalidDataException("NetworkConfiguration must be set in CfnExpressGatewayServiceProps and of type ExpressGatewayServiceNetworkConfigurationProperty");
        }
        else
        {
            networkConfiguration = new ExpressGatewayServiceNetworkConfigurationProperty();
            props.NetworkConfiguration = networkConfiguration;
        }
        
        var environmentVariables = new List<IKeyValuePairProperty>();

        var existingKvp = primaryContainer.Environment as IKeyValuePairProperty[];
        if (existingKvp != null)
        {
            environmentVariables.AddRange(existingKvp);
        }
        
        var securityGroupIdSet = new HashSet<string>();
        if (networkConfiguration.SecurityGroups != null)
        {
            foreach (var securityGroup in networkConfiguration.SecurityGroups)
            {
                securityGroupIdSet.Add(securityGroup);
            }
        }
        
        var allLinkReferences = GetAllReferencesLink(resource);
        foreach (var linkAnnotation in allLinkReferences)
        {
            var results =
                linkAnnotation.PublishTarget.GetAllReferences(linkAnnotation.Resource, linkAnnotation.LinkedConstruct);

            if (results.EnvironmentVariables != null)
            {
                environmentVariables.AddRange(results.EnvironmentVariables.Select(x => new KeyValuePairProperty
                {
                    Name = x.Key,
                    Value = x.Value
                }));
            }

            linkAnnotation.PublishTarget.ApplyReferenceSecurityGroup(linkAnnotation, environmentResource.DefaultsProvider.GetDefaultECSClusterSecurityGroup());
        }

        primaryContainer.Environment = environmentVariables.ToArray();
        networkConfiguration.SecurityGroups = securityGroupIdSet.ToArray();
    }
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateExpressServiceConfig
{
    public Action<CfnExpressGatewayServiceProps>? PropsCfnExpressGatewayServicePropsCallback { get; set; }

    public Action<CfnExpressGatewayService>? ConstructCfnExpressGatewayServiceCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishECSFargateServiceExpressAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateExpressServiceConfig Config { get; init; } = new PublishECSFargateExpressServiceConfig();
}