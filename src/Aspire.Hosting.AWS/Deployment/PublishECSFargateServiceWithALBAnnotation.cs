// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS.Patterns;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishECSFargateServiceWithALBAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateServiceWithALBConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing an ECS Fargate Service with an Application Load Balancer
/// </summary>

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateServiceWithALBConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the ApplicationLoadBalancedTaskImageOptions
    /// </summary>
    public PublishCallback<ApplicationLoadBalancedTaskImageOptions>? PropsApplicationLoadBalancedTaskImageOptionsCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed ApplicationLoadBalancedTaskImageOptions
    /// </summary>
    public PublishCallback<ApplicationLoadBalancedFargateServiceProps>? PropsApplicationLoadBalancedFargateServiceCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed ApplicationLoadBalancedFargateService
    /// </summary>
    public PublishCallback<ApplicationLoadBalancedFargateService>? ConstructApplicationLoadBalancedFargateServiceCallback { get; set; }

}

