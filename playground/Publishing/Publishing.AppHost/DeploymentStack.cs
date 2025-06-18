using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
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

        ECSCluster = new Cluster(this, "ECSCluster", new ClusterProps
        {
            Vpc = MainVpc
        });

        LambdaQueue = new Queue(this, "LambdaQueue");

        ElastiCacheSubnetGroup = new CfnSubnetGroup(this, "RedisSubnetGroup", new CfnSubnetGroupProps
        {
            Description = "Subnet group for cluster",
            SubnetIds = MainVpc.PrivateSubnets.Select(subnet => subnet.SubnetId).ToArray()
        });

        ElastiCacheParameterGroup = new CfnParameterGroup(this, "RedisParameterGroup", new CfnParameterGroupProps
        {
            CacheParameterGroupFamily = "redis7",
            Description = "Parameter group for Redis cluster",
            Properties = new Dictionary<string, string>
            {
                { "maxmemory-policy", "volatile-lru" }
            }
        });
    }

    public IVpc MainVpc { get; private set; }

    public SecurityGroup DefaultSecurityGroup { get; private set; }

    public Queue LambdaQueue {  get; private set; }

    public Cluster ECSCluster { get; private set; }

    public CfnSubnetGroup ElastiCacheSubnetGroup { get; private set; }

    public CfnParameterGroup ElastiCacheParameterGroup { get; private set; }
}
