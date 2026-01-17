using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.SQS;
using Aspire.Hosting.AWS.Deployment;
using Constructs;

namespace Lambda.AppHost;

public class DeploymentStack : Stack
{
    public DeploymentStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        LambdaQueue = new Queue(this, "LambdaQueue");

        DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
        {
            IsDefault = true
        });
    }

    public Queue LambdaQueue {  get; private set; }

    [DefaultVpc]
    public IVpc DefaultVpc { get; private set; }
}
