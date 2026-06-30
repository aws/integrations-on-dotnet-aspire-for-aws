// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#if NET10_0_OR_GREATER

using Amazon.CDK.AWS.BedrockAgentCore;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishAgentCoreRuntimeAnnotation : IAWSPublishTargetAnnotation
{
    public PublishAgentCoreRuntimeConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing a Bedrock AgentCore Runtime
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishAgentCoreRuntimeConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the AgentCore Runtime
    /// </summary>
    public PublishCallback<CfnRuntimeProps>? PropsCfnRuntimeCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed AgentCore Runtime
    /// </summary>
    public PublishCallback<CfnRuntime>? ConstructCfnRuntimeCallback { get; set; }

    /// <summary>
    /// The subnet IDs to place the runtime in when it is attached to a VPC. A runtime is attached to a
    /// VPC only when it references a resource that requires VPC access (for example an ElastiCache
    /// cluster); otherwise the runtime uses public networking and this property is ignored.
    /// <para>
    /// When not set, the subnets of the default VPC are used. Bedrock AgentCore only supports a subset of
    /// a region's availability zones, so the default VPC's subnets may include an availability zone that
    /// AgentCore does not support, which causes deployment to fail with an error similar to
    /// "The following subnets are in unsupported availability zones". Set this property to the subnet IDs
    /// in AgentCore-supported availability zones to resolve such failures. The supported availability
    /// zones are not known at synthesis time, so this selection cannot be made automatically.
    /// </para>
    /// </summary>
    public string[]? VpcSubnetIds { get; set; }
}

#endif
