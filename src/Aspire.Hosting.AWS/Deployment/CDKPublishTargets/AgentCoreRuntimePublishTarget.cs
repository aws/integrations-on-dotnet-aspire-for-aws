// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#if NET10_0_OR_GREATER

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.BedrockAgentCore;
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
            AgentRuntimeName = projectResource.Name,
            AgentRuntimeArtifact = new CfnRuntime.AgentRuntimeArtifactProperty
            {
                ContainerConfiguration = new CfnRuntime.ContainerConfigurationProperty
                {
                    ContainerUri = asset.ImageUri
                }
            },
            EnvironmentVariables = new Dictionary<string, string>()
        };

        var referencePoints = new AgentCoreRuntimeConnectionPoints(runtimeProps);
        ProcessRelationShips(referencePoints, projectResource, environment);

        publishAnnotation.Config.PropsCfnRuntimeCallback?.Invoke(CreatePublishTargetContext(environment), runtimeProps);
        environment.DefaultsProvider.ApplyAgentCoreRuntimeDefaults(runtimeProps);

        var runtime = new CfnRuntime(environment.CDKStack, $"AgentRuntime-{projectResource.Name}", runtimeProps);
        publishAnnotation.Config.ConstructCfnRuntimeCallback?.Invoke(CreatePublishTargetContext(environment), runtime);

        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, runtime, this);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is ProjectResource &&
            resource.Annotations.OfType<AgentCoreRuntimeAnnotation>().Any())
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
        //   AWS:Resources:{agentName}:AgentRuntimeArn  ->  AWS_RESOURCES__{AGENT}__AGENTRUNTIMEARN
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
internal class AgentCoreRuntimeConnectionPoints(CfnRuntimeProps props) : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.EnvironmentVariables as IDictionary<string, string> ?? new Dictionary<string, string>();
        set => props.EnvironmentVariables = value ?? new Dictionary<string, string>();
    }
}

#endif
