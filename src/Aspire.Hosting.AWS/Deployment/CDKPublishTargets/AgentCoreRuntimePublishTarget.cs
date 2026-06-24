// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#if NET10_0_OR_GREATER

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.BedrockAgentCore;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.AgentCore;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class AgentCoreRuntimePublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<AgentCoreRuntimePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "Bedrock AgentCore Runtime";

    public override Type PublishTargetAnnotation => typeof(PublishAgentCoreRuntimeAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid ProjectResource.");

        var publishAnnotation = annotation as PublishAgentCoreRuntimeAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishAgentCoreRuntimeAnnotation)}.");

        var imageTarballPath = await imageBuilder.CreateTarballImageAsync(projectResource, cancellationToken);

        var asset = new TarballImageAsset(environment.CDKStack, $"ContainerTarBall-{projectResource.Name}", new TarballImageAssetProps
        {
            TarballFile = imageTarballPath
        });

        var runtimeProps = new CfnRuntimeProps
        {
            AgentRuntimeName = $"{environment.CDKStack.StackName}_{projectResource.Name}",
            AgentRuntimeArtifact = new CfnRuntime.AgentRuntimeArtifactProperty
            {
                ContainerConfiguration = new CfnRuntime.ContainerConfigurationProperty
                {
                    ContainerUri = asset.ImageUri
                }
            },
            EnvironmentVariables = new Dictionary<string, string>(),            
        };

        publishAnnotation.Config.PropsCfnRuntimeCallback?.Invoke(CreatePublishTargetContext(environment), runtimeProps);
        environment.DefaultsProvider.ApplyAgentCoreRuntimeDefaults(runtimeProps);

        var referencePoints = new AgentCoreRuntimeConnectionPoints(
            runtimeProps,
            environment,
            () => CreateReferenceSecurityGroup(environment, projectResource),
            publishAnnotation.Config.VpcSubnetIds,
            projectResource.Name,
            logger);
        ProcessRelationShips(referencePoints, projectResource, environment);

        var runtime = new CfnRuntime(environment.CDKStack, $"AgentRuntime-{projectResource.Name}", runtimeProps);
        publishAnnotation.Config.ConstructCfnRuntimeCallback?.Invoke(CreatePublishTargetContext(environment), runtime);

        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, runtime, this);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is ProjectResource &&
            resource.Annotations.OfType<AgentCoreRuntimeAnnotation>().Any() &&
             cdkDefaultsProvider.DefaultAgentCoreProjectResourcePublishTarget == CDKDefaultsProvider.AgentCoreProjectResourcePublishTarget.AgentCoreRuntime)
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishAgentCoreRuntimeAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 200
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not CfnRuntime runtime)
            return result;

        // Expose the runtime ARN to referencing apps under the standard reference convention:
        //   AWS:Resources:{agentName}:AgentRuntimeArn  ->  AWS__Resources__{agentName}__AgentRuntimeArn
        // AttrAgentRuntimeArn resolves to a CloudFormation Fn::GetAtt token during synthesis.
        var prefix = $"{Constants.DefaultConfigSection}:{linkedAnnotation.Resource.Name}".ToEnvironmentVariables();
        result.EnvironmentVariables = new Dictionary<string, string>
        {
            [$"{prefix}__{Constants.AgentRuntimeArnOutputName}"] = runtime.AttrAgentRuntimeArn
        };
        return result;
    }

    /// <inheritdoc/>
    public override bool ReferenceRequiresTaskRolePolicy() => true;

    /// <inheritdoc/>
    public override void ApplyReferenceTaskRolePolicy(AWSLinkedObjectsAnnotation linkedAnnotation, IRole taskRole)
    {
        if (linkedAnnotation.Construct is not CfnRuntime runtime)
            return;

        // A resource referencing this agent invokes it at runtime via the AgentCore invoke APIs, so its
        // task role needs permission to call them. The policy is scoped to this runtime and its child
        // endpoints (named qualifiers) using the runtime ARN, which resolves to an Fn::GetAtt token.
        var runtimeArn = runtime.AttrAgentRuntimeArn;
        taskRole.AddToPrincipalPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "bedrock-agentcore:Invoke*" },
            Resources = new[]
            {
                runtimeArn,
                Fn.Join("", new[] { runtimeArn, "/*" })
            }
        }));
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class AgentCoreRuntimeConnectionPoints(
    CfnRuntimeProps props,
    AWSCDKEnvironmentResource environment,
    Func<ISecurityGroup> securityGroupFactory,
    string[]? configuredSubnetIds,
    string resourceName,
    ILogger logger) : AbstractCDKConstructConnectionPoints
{
    private ISecurityGroup? _referenceSecurityGroup;
    private IVpc? _vpc;

    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.EnvironmentVariables as IDictionary<string, string> ?? new Dictionary<string, string>();
        set => props.EnvironmentVariables = value ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Setting the VPC switches the runtime's network configuration from the default PUBLIC mode to VPC
    /// mode. AgentCore runtimes only support a single network mode, so a VPC-requiring reference (e.g.
    /// ElastiCache) forces VPC mode. Subnets are resolved in priority order: the subnets configured on
    /// the publish config, then subnets already set on the network config (e.g. via a props callback),
    /// then the default VPC's subnets.
    /// </summary>
    public override IVpc? Vpc
    {
        get => _vpc;
        set
        {
            _vpc = value;
            if (value == null)
                return;

            var vpcConfig = GetOrCreateVpcConfig();

            if (configuredSubnetIds is { Length: > 0 })
            {
                vpcConfig.Subnets = configuredSubnetIds;
            }
            else if (vpcConfig.Subnets is string[] { Length: > 0 })
            {
                // Subnets already provided via a PropsCfnRuntimeCallback; leave them as-is.
            }
            else
            {
                vpcConfig.Subnets = environment.DefaultsProvider.GetDefaultVpcSubnetIds();

                // Bedrock AgentCore only supports a subset of a region's availability zones, and that set
                // is not known at synthesis time, so the default VPC's subnets cannot be filtered
                // automatically. Mirror the ElastiCache cluster-mode logging so the user knows how to react
                // if deployment fails because a default subnet is in an unsupported availability zone.
                logger.LogInformation("Placing AgentCore runtime {Resource} in the default VPC's subnets. If deployment fails with an error about subnets in unsupported availability zones, set the {Property} property on {Config} to subnet IDs in AgentCore-supported availability zones.", resourceName, nameof(PublishAgentCoreRuntimeConfig.VpcSubnetIds), nameof(PublishAgentCoreRuntimeConfig));

                // When the VPC has no private subnets the runtime is placed in public subnets. Like a
                // Lambda function, a runtime in a public subnet is not assigned a public IP address and so
                // cannot reach the internet or public AWS service endpoints (e.g. Bedrock, ECR, CloudWatch
                // Logs) it needs at runtime. Warn so the user can attach private subnets with a NAT gateway.
                if (!value.PrivateSubnets.Any())
                {
                    logger.LogWarning("AgentCore runtime {Resource} references a resource that requires the runtime to be attached to a VPC, but the configured VPC contains only public subnets and no private subnets. A runtime placed in public subnets is not assigned a public IP address and therefore cannot reach the internet or public AWS service endpoints (such as Bedrock, ECR, and CloudWatch Logs). To allow that access through a NAT Gateway, attach the runtime to private subnets by setting the {Property} property on {Config} to private subnet IDs.", resourceName, nameof(PublishAgentCoreRuntimeConfig.VpcSubnetIds), nameof(PublishAgentCoreRuntimeConfig));
                }
            }
        }
    }

    /// <summary>
    /// Lazily creates the reference security group for the runtime and adds its id to the VPC network
    /// configuration. Referenced resources add this security group as an ingress rule to allow access.
    /// </summary>
    public override ISecurityGroup? ReferenceSecurityGroup
    {
        get
        {
            if (_referenceSecurityGroup == null)
            {
                _referenceSecurityGroup = securityGroupFactory();

                var vpcConfig = GetOrCreateVpcConfig();
                var existing = vpcConfig.SecurityGroups as string[] ?? [];
                vpcConfig.SecurityGroups = existing.Append(_referenceSecurityGroup.SecurityGroupId).ToArray();
            }

            return _referenceSecurityGroup;
        }
    }

    /// <summary>
    /// Ensures the runtime props carry a VPC network configuration, switching <c>NetworkMode</c> to VPC,
    /// and returns the <see cref="CfnRuntime.VpcConfigProperty"/> to populate.
    /// </summary>
    private CfnRuntime.VpcConfigProperty GetOrCreateVpcConfig()
    {
        if (props.NetworkConfiguration is not CfnRuntime.NetworkConfigurationProperty networkConfig)
        {
            networkConfig = new CfnRuntime.NetworkConfigurationProperty();
            props.NetworkConfiguration = networkConfig;
        }

        networkConfig.NetworkMode = "VPC";

        if (networkConfig.NetworkModeConfig is not CfnRuntime.VpcConfigProperty vpcConfig)
        {
            vpcConfig = new CfnRuntime.VpcConfigProperty();
            networkConfig.NetworkModeConfig = vpcConfig;
        }

        return vpcConfig;
    }
}

#endif
