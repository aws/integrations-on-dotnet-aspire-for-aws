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
    /// Callback to modify the properties used to construct the AgentCore Memory. Only invoked when an
    /// AgentCore Memory resource is created for the runtime (see <see cref="CreateMemory"/>).
    /// </summary>
    public PublishCallback<CfnMemoryProps>? PropsCfnMemoryCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed AgentCore Memory. Only invoked when an AgentCore Memory resource
    /// is created for the runtime (see <see cref="CreateMemory"/>).
    /// </summary>
    public PublishCallback<CfnMemory>? ConstructCfnMemoryCallback { get; set; }

    /// <summary>
    /// Controls whether an <c>AWS::BedrockAgentCore::Memory</c> resource is provisioned for the runtime
    /// during deployment.
    /// <para>
    /// When <c>null</c> (the default) memory creation follows whether <c>WithAgentCoreMemory()</c> was called on
    /// the agent. Set to <c>true</c> or <c>false</c> to force or suppress memory creation during
    /// deployment independently of local testing — for example call <c>WithAgentCoreMemory()</c> to use the memory
    /// emulator locally while setting this to <c>false</c> to skip provisioning memory when deployed.
    /// </para>
    /// </summary>
    public bool? CreateMemory { get; set; }

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
