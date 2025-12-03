using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.AWS.Environments;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateExpressConfig
{
    public Action<CfnExpressGatewayServiceProps>? PropsCfnExpressGatewayServicePropsCallback { get; set; }

    public Action<CfnExpressGatewayService>? ConstructCfnExpressGatewayServiceCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKECSFargateExpressAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public PublishCDKECSFargateExpressConfig Config { get; init; } = new PublishCDKECSFargateExpressConfig();
}
