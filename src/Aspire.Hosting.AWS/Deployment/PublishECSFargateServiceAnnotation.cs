// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishECSFargateServiceAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateServiceConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing an ECS Fargate Service
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateServiceConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the Container Definition
    /// </summary>
    public PublishCallback<ContainerDefinitionProps>? PropsContainerDefinitionCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed Container Definition
    /// </summary>
    public PublishCallback<ContainerDefinition>? ConstructContainerDefinitionCallback { get; set; }

    /// <summary>
    /// Callback to modify the properties used to construct the Fargate Task Definition
    /// </summary>
    public PublishCallback<FargateTaskDefinitionProps>? PropsFargateTaskDefinitionCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed Fargate Task Definition
    /// </summary>
    public PublishCallback<FargateTaskDefinition>? ConstructFargateTaskDefinitionCallback { get; set; }

    /// <summary>
    /// Callback to modify the properties used to construct the Fargate Service
    /// </summary>
    public PublishCallback<FargateServiceProps>? PropsFargateServiceCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed Fargate Service
    /// </summary>
    public PublishCallback<FargateService>? ConstructFargateServiceCallback { get; set; }
}
