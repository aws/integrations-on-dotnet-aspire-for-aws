// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateConfig
{
    public required Cluster ECSCluster { get; init; }

    public int DesiredCount { get; init; } = 1;

    public double MinHealthyPercent { get; init; } = 100;

    public Action<ContainerDefinitionProps>? PropsContainerDefinitionCallback { get; set; }

    public Action<ContainerDefinition>? ConstructContainerDefinitionCallback { get; set; }

    public Action<FargateTaskDefinitionProps>? PropsFargateTaskDefinitionCallback { get; set; }

    public Action<FargateTaskDefinition>? ConstructFargateTaskDefinitionCallback { get; set; }

    public Action<FargateServiceProps>? PropsFargateServiceCallback { get; set; }

    public Action<FargateService>? ConstructFargateServiceCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKECSFargateAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public required PublishCDKECSFargateConfig Config { get; init; }
}
