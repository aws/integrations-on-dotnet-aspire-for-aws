using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.AWS.Environments.DefaultProviderImplementations;
using Aspire.Hosting.AWS.Utils;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class DefaultProvider
{
    public static readonly DefaultProvider V1 = new V1DefaultProvider();

    protected DefaultProvider()
    {
    }

    public virtual string DeploymentTagName => "aspire:deployment-tag";

    #region LambdaFunction
    public virtual double? LambdaFunctionMemorySize => 512;

    internal protected virtual void ApplyLambdaFunctionDefaults(string projectPath, FunctionProps props)
    {
        if (!props.MemorySize.HasValue)
            props.MemorySize = LambdaFunctionMemorySize;

        if (props.Runtime == null)
        {
            var targetFramework = ProjectUtilities.LookupTargetFrameworkFromProjectFile(projectPath);
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new InvalidOperationException($"Unable to determine target .NET version for Lambda function.");
            }

            switch (targetFramework)
            {
                case "net8.0":
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                case "net9.0":
                    // Fallback to .NET 8 for non-LTS assuming deployment package will be self contained.
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported target framework '{targetFramework}' for Lambda function.");
            }
        }
    }

    #endregion

    #region ECSFargateServiceWithALB
    public virtual double? ECSFargateServiceWithALBCpu => 256;

    public virtual double? ECSFargateServiceWithALBMemoryLimitMiB => 512;

    public virtual double? ECSFargateServiceWithALBDesiredCount => 3;

    public virtual double? ECSFargateServiceWithALBListenerPort => 80;

    public virtual double? ECSFargateServiceWithALBContainerPort => 8080;

    public virtual bool? ECSFargateServiceWithALBPublicLoadBalancer => true;

    public virtual double? ECSFargateServiceWithALBMinHealthyPercent => 100;

    internal protected virtual void ApplyECSFargateServiceWithALBDefaults(ApplicationLoadBalancedTaskImageOptions props)
    {
        if (!props.ContainerPort.HasValue)
            props.ContainerPort = ECSFargateServiceWithALBContainerPort;
    }

    internal protected virtual void ApplyECSFargateServiceWithALBDefaults(AWSCDKEnvironmentResource environment, ApplicationLoadBalancedFargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = environment.DeploymentConstructProvider.GetDefaultECSCluster();
        if (!props.Cpu.HasValue)
            props.Cpu = ECSFargateServiceWithALBCpu;
        if (!props.MemoryLimitMiB.HasValue)
            props.MemoryLimitMiB = ECSFargateServiceWithALBMemoryLimitMiB;
        if (!props.DesiredCount.HasValue)
            props.DesiredCount = ECSFargateServiceWithALBDesiredCount;
        if (!props.ListenerPort.HasValue)
            props.ListenerPort = ECSFargateServiceWithALBListenerPort;
        if (!props.PublicLoadBalancer.HasValue)
            props.PublicLoadBalancer = ECSFargateServiceWithALBPublicLoadBalancer;
        if (!props.MinHealthyPercent.HasValue)
            props.MinHealthyPercent = ECSFargateServiceWithALBMinHealthyPercent;
        if (props.SecurityGroups == null || props.SecurityGroups.Length == 0)
        {
            var defaultSecurityGroup = environment.DeploymentConstructProvider.GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }
    }

    #endregion

    #region ECSFargateService

    public virtual double? ECSFargateServiceCpu => 256;

    public virtual double? ECSFargateServiceMemoryLimitMiB => 512;

    public virtual double? ECSFargateServiceDesiredCount => 1;

    public virtual double? ECSFargateServiceMinHealthyPercent => 100;

    public virtual LogDriver? CreateECSFargateServiceLogDriver(AWSCDKEnvironmentResource environment, string projectName)
    {
        return LogDrivers.AwsLogs(new AwsLogDriverProps
        {
            StreamPrefix = environment.CDKStack.StackName + "/" + projectName
        });
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(FargateTaskDefinitionProps props)
    {
        if (props.Cpu == null)
            props.Cpu = ECSFargateServiceCpu;
        if (props.MemoryLimitMiB == null)
            props.MemoryLimitMiB = ECSFargateServiceMemoryLimitMiB;
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(AWSCDKEnvironmentResource environment, string projectName, ContainerDefinitionProps props)
    {
        if (props.Logging == null)
            props.Logging = CreateECSFargateServiceLogDriver(environment, projectName);
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(AWSCDKEnvironmentResource environment, FargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = environment.DeploymentConstructProvider.GetDefaultECSCluster();
        if (!props.DesiredCount.HasValue)
            props.DesiredCount = ECSFargateServiceDesiredCount;
        if (!props.MinHealthyPercent.HasValue)
            props.MinHealthyPercent = ECSFargateServiceMinHealthyPercent;
        if (props.SecurityGroups == null || props.SecurityGroups.Length == 0)
        {
            var defaultSecurityGroup = environment.DeploymentConstructProvider.GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }
    }

    #endregion

    #region ElasticCacheCluster
    public virtual string ElasticCacheClusterReplicationGroupDescription => "Cache for Aspire Application";

    public virtual string ElasticCacheClusterEngine => "redis";

    public virtual string? ElasticCacheClusterEngineVersion => "7.1";

    public virtual string? ElasticCacheClusterCacheNodeType => "cache.t3.micro";

    public virtual double? ElasticCacheClusterNumCacheClusters => 2;

    public virtual bool? ElasticCacheClusterAutomaticFailoverEnabled => true;

    public virtual double ElasticCacheClusterPort => 6379;

    public virtual string ElasticCacheClusterSubnetGroupDescription => "Subnet group for ElastiCache cluster";

    public virtual string ElasticCacheClusterParameterGroupFamily => "redis7";

    public virtual string ElasticCacheClusterParameterGroupDescription => "Parameter group for Redis cluster";

    public virtual IDictionary<string, string>? ElasticCacheClusterParameterGroupProperties => new Dictionary<string, string>
    {
        { "maxmemory-policy", "volatile-lru" }
    };

    internal protected virtual void ApplyCfnReplicationGroupPropsDefaults(AWSCDKEnvironmentResource environment, CfnReplicationGroupProps props)
    {
        if (props.ReplicationGroupDescription == null)
            props.ReplicationGroupDescription = ElasticCacheClusterReplicationGroupDescription;
        if (props.CacheNodeType == null)
            props.CacheNodeType = ElasticCacheClusterCacheNodeType;
        if (props.Engine == null)
            props.Engine = ElasticCacheClusterEngine;
        if (props.EngineVersion == null)
            props.EngineVersion = ElasticCacheClusterEngineVersion;
        if (props.NumCacheClusters == null)
            props.NumCacheClusters = ElasticCacheClusterNumCacheClusters;
        if (props.AutomaticFailoverEnabled == null)
            props.AutomaticFailoverEnabled = ElasticCacheClusterAutomaticFailoverEnabled;
        if (props.Port == null)
            props.Port = ElasticCacheClusterPort;

        if (props.CacheSubnetGroupName == null)
            props.CacheSubnetGroupName = environment.DeploymentConstructProvider.GetDefaultElastiCacheCfnSubnetGroup().Ref;
        if (props.CacheParameterGroupName == null)
            props.CacheParameterGroupName = environment.DeploymentConstructProvider.GetDefaultElastiCacheCfnParameterGroup().Ref;
        if (props.SecurityGroupIds == null)
            props.SecurityGroupIds = new[] { environment.DeploymentConstructProvider.GetDefaultElastiCacheSecurityGroup().SecurityGroupId };
    }

    #endregion

    #region Default CDK Constructions

    internal protected virtual IVpc CreateDefaultVpc(AWSCDKEnvironmentResource environment)
    {
        return new Vpc(environment.CDKStack, "DefaultVPC", new VpcProps
        {
            MaxAzs = 2
        });
    }

    internal protected virtual ICluster CreateDefaultECSCluster(AWSCDKEnvironmentResource environment)
    {
        return new Cluster(environment.CDKStack, "DefaultECSCluster", new ClusterProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc()
        });
    }

    internal protected virtual ISecurityGroup CreateDefaultECSClusterSecurityGroup(AWSCDKEnvironmentResource environment)
    {
        return new SecurityGroup(environment.CDKStack, "DefaultECSClusterSecurityGroup", new SecurityGroupProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc(),
            AllowAllOutbound = true
        });
    }

    internal protected virtual CfnSubnetGroup CreateDefaultElastiCacheCfnSubnetGroup(AWSCDKEnvironmentResource environment)
    {
        var subnetIds = environment.DeploymentConstructProvider.GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId).ToArray();
        return new CfnSubnetGroup(environment.CDKStack, "DefaultElastiCacheSubnetGroup", new CfnSubnetGroupProps
        {
            Description = environment.DefaultValuesProvider.ElasticCacheClusterSubnetGroupDescription,
            SubnetIds = subnetIds
        });
    }

    internal protected virtual CfnParameterGroup CreateDefaultElastiCacheCfnParameterGroup(AWSCDKEnvironmentResource environment)
    {
        return new CfnParameterGroup(environment.CDKStack, "DefaultElastiCacheParameterGroup", new CfnParameterGroupProps
        {
            CacheParameterGroupFamily = environment.DefaultValuesProvider.ElasticCacheClusterParameterGroupFamily,
            Description = environment.DefaultValuesProvider.ElasticCacheClusterParameterGroupDescription,
            Properties = environment.DefaultValuesProvider.ElasticCacheClusterParameterGroupProperties
        });
    }

    internal protected virtual ISecurityGroup CreateDefaultElastiCacheSecurityGroup(AWSCDKEnvironmentResource environment)
    {
        var defaultElastiCacheSecurityGroup = new SecurityGroup(environment.CDKStack, "DefaultElastiCacheSecurityGroup", new SecurityGroupProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc(),
            AllowAllOutbound = true
        });

        defaultElastiCacheSecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(environment.DefaultValuesProvider.ElasticCacheClusterPort), "Allow Redis access");
        return defaultElastiCacheSecurityGroup;
    }

    #endregion
}

