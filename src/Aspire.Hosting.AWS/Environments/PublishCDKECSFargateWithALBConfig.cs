// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateWithALBConfig
{
    public required Cluster ECSCluster { get; init; }

    public Action<ApplicationLoadBalancedFargateServiceProps>? PropsApplicationLoadBalancedFargateServiceCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateService>? ConstructApplicationLoadBalancedFargateServiceCallback { get; set; }

}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKECSFargateWithALBAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public required PublishCDKECSFargateWithALBConfig Config { get; init; }
}
