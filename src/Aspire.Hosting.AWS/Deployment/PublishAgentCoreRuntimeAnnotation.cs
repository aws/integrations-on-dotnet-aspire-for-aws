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
}

#endif
