// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateServiceExpressConfig
{
    public Action<CfnExpressGatewayServiceProps>? PropsCfnExpressGatewayServicePropsCallback { get; set; }

    public Action<CfnExpressGatewayService>? ConstructCfnExpressGatewayServiceCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateServiceExpressAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public PublishCDKECSFargateServiceExpressConfig Config { get; init; } = new PublishCDKECSFargateServiceExpressConfig();
}
