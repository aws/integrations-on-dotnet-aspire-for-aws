// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;

namespace Aspire.Hosting.AWS.Environments;

public class PublishCDKECSFargateWithALBConfig
{
    public required Cluster ECSCluster { get; init; }

    public Action<ApplicationLoadBalancedFargateServiceProps>? PropsApplicationLoadBalancedFargateServiceCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateService>? ConstructApplicationLoadBalancedFargateServiceCallback { get; set; }

}

internal class PublishCDKECSFargateWithALBAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public required PublishCDKECSFargateWithALBConfig Config { get; init; }
}
