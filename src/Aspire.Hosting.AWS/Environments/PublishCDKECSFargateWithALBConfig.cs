// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS.Patterns;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateWithALBConfig
{
    public Action<ApplicationLoadBalancedTaskImageOptions>? PropsApplicationLoadBalancedTaskImageOptionsCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateServiceProps>? PropsApplicationLoadBalancedFargateServiceCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateService>? ConstructApplicationLoadBalancedFargateServiceCallback { get; set; }

}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKECSFargateWithALBAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public PublishCDKECSFargateWithALBConfig Config { get; init; } = new PublishCDKECSFargateWithALBConfig();
}
