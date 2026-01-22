using Amazon.CDK;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace DeploymentTestApp.AppHost;

internal class PublishLambdaWithCustomization : Stack
{
    public PublishLambdaWithCustomization(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        LambdaQueue = new Queue(this, "LambdaQueue");
    }

    public Queue LambdaQueue { get; private set; }
}
