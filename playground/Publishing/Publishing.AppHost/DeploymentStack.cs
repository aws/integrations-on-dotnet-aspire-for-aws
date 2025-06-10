using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Lambda.AppHost;

public class DeploymentStack : Stack
{
    public DeploymentStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        MainVpc = new Vpc(this, "Vpc", new VpcProps
        {
            MaxAzs = 2 
        });

        // Security Group for Redis
        DefaultSecurityGroup = new SecurityGroup(this, "DefaultSecurityGroup", new SecurityGroupProps
        {
            Vpc = MainVpc,
            AllowAllOutbound = true
        });

        DefaultSecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(6379), "Allow Redis access");

        LambdaQueue = new Queue(this, "LambdaQueue");
    }

    public IVpc MainVpc { get; private set; }

    public SecurityGroup DefaultSecurityGroup { get; private set; }

    public Queue LambdaQueue {  get; private set; }
}
