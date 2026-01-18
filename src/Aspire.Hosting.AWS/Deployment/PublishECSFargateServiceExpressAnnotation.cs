// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishECSFargateServiceExpressAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateExpressServiceConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing an ECS Fargate Express Service
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateExpressServiceConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the CfnExpressGatewayService
    /// </summary>
    public PublishCallback<CfnExpressGatewayServiceProps>? PropsCfnExpressGatewayServicePropsCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed CfnExpressGatewayService
    /// </summary>
    public PublishCallback<CfnExpressGatewayService>? ConstructCfnExpressGatewayServiceCallback { get; set; }
}
