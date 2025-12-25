// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.IAM;
using System.Reflection;

namespace Aspire.Hosting.AWS.Environments.CDKDefaultsProviders;

public partial class CDKDefaultsProvider
{
    private IVpc? _defaultVpc;
    public IVpc GetDefaultVpc()
    {
        if (_defaultVpc == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultVpcAttribute, IVpc>();
            if(definedDefault != null)
            {
                _defaultVpc = definedDefault;
            }
            else
            {
                _defaultVpc = CreateDefaultVpc();
            }
        }

        return _defaultVpc;
    }
    
    protected virtual IVpc CreateDefaultVpc()
    {
        return new Vpc(EnvironmentResource.CDKStack, "DefaultVPC", new VpcProps
        {
            MaxAzs = 2
        });
    }

    private ICluster? _defaultECSCluster;
    public ICluster GetDefaultECSCluster()
    {
        if (_defaultECSCluster == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultECSClusterAttribute, ICluster>();
            if (definedDefault != null)
            {
                _defaultECSCluster = definedDefault;
            }
            else
            {
                _defaultECSCluster = CreateDefaultECSCluster();
            }
        }

        return _defaultECSCluster;
    }
    
    protected virtual ICluster CreateDefaultECSCluster()
    {
        return new Cluster(EnvironmentResource.CDKStack, "DefaultECSCluster", new ClusterProps
        {
            Vpc = GetDefaultVpc()
        });
    }
    
    private ISecurityGroup? _defaultECSClusterSecurityGroup;
    public ISecurityGroup GetDefaultECSClusterSecurityGroup()
    {
        if (_defaultECSClusterSecurityGroup == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultECSClusterSecurityGroupAttribute, ISecurityGroup>();
            if (definedDefault != null)
            {
                _defaultECSClusterSecurityGroup = definedDefault;
            }
            else
            {
                _defaultECSClusterSecurityGroup = CreateDefaultECSClusterSecurityGroup();
            }
        }
        return _defaultECSClusterSecurityGroup;
    }    

    protected virtual ISecurityGroup CreateDefaultECSClusterSecurityGroup()
    {
        return new SecurityGroup(EnvironmentResource.CDKStack, "DefaultECSClusterSecurityGroup", new SecurityGroupProps
        {
            Vpc = GetDefaultVpc(),
            AllowAllOutbound = true
        });
    }
    
    private CfnSubnetGroup? _defaultElastiCacheCfnSubnetGroup;
    public CfnSubnetGroup GetDefaultElastiCacheCfnSubnetGroup()
    {
        if (_defaultElastiCacheCfnSubnetGroup == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultElastiCacheCfnSubnetGroupAttribute, CfnSubnetGroup>();
            if (definedDefault != null)
            {
                _defaultElastiCacheCfnSubnetGroup = definedDefault;
            }
            else
            {
                _defaultElastiCacheCfnSubnetGroup = CreateDefaultElastiCacheCfnSubnetGroup();
            }
        }
        return _defaultElastiCacheCfnSubnetGroup;
    }    

    protected virtual CfnSubnetGroup CreateDefaultElastiCacheCfnSubnetGroup()
    {
        var subnetIds = GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId).ToArray();
        return new CfnSubnetGroup(EnvironmentResource.CDKStack, "DefaultElastiCacheSubnetGroup", new CfnSubnetGroupProps
        {
            Description = ElasticCacheNodeClusterSubnetGroupDescription,
            SubnetIds = subnetIds
        });
    }
    
    private CfnParameterGroup? _defaultElastiCacheCfnParameterGroup;
    public CfnParameterGroup GetDefaultElastiCacheCfnParameterGroup()
    {
        if (_defaultElastiCacheCfnParameterGroup == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultElastiCacheCfnParameterGroupAttribute, CfnParameterGroup>();
            if (definedDefault != null)
            {
                _defaultElastiCacheCfnParameterGroup = definedDefault;
            }
            else
            {
                _defaultElastiCacheCfnParameterGroup = CreateDefaultElastiCacheCfnParameterGroup();
            }
        }

        return _defaultElastiCacheCfnParameterGroup;
    }    

    protected virtual CfnParameterGroup CreateDefaultElastiCacheCfnParameterGroup()
    {
        return new CfnParameterGroup(EnvironmentResource.CDKStack, "DefaultElastiCacheParameterGroup", new CfnParameterGroupProps
        {
            CacheParameterGroupFamily = ElasticCacheNodeClusterParameterGroupFamily,
            Description = ElasticCacheNodeClusterParameterGroupDescription,
            Properties = ElasticCacheNodeClusterParameterGroupProperties
        });
    }
    
    private ISecurityGroup? _defaultElastiCacheSecurityGroup;
    public ISecurityGroup GetDefaultElastiCacheSecurityGroup()
    {
        if (_defaultElastiCacheSecurityGroup == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultElastiCacheSecurityGroupAttribute, ISecurityGroup>();
            if (definedDefault != null)
            {
                _defaultElastiCacheSecurityGroup = definedDefault;
            }
            else
            {
                _defaultElastiCacheSecurityGroup = CreateDefaultElastiCacheSecurityGroup();
            }
        }

        return _defaultElastiCacheSecurityGroup;
    }    

    protected virtual ISecurityGroup CreateDefaultElastiCacheSecurityGroup()
    {
        var defaultElastiCacheSecurityGroup = new SecurityGroup(EnvironmentResource.CDKStack, "DefaultElastiCacheSecurityGroup", new SecurityGroupProps
        {
            Vpc = GetDefaultVpc(),
            AllowAllOutbound = true
        });

        defaultElastiCacheSecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(ElasticCacheNodeClusterPort), "Allow Redis access");
        return defaultElastiCacheSecurityGroup;
    }
    
    private IRole? _defaultECSExpressExecutionRole;
    public IRole GetDefaultECSExpressExecutionRole()
    {
        if (_defaultECSExpressExecutionRole == null)
        {
            var definedDefault = FindDefaultConstructByAttribute<DefaultECSExpressExecutionRoleAttribute, IRole>();
            if (definedDefault != null)
            {
                _defaultECSExpressExecutionRole = definedDefault;
            }
            else
            {
                _defaultECSExpressExecutionRole = CreateDefaultECSExpressExecutionRole();
            }
        }
        return _defaultECSExpressExecutionRole;
    }    

    protected virtual IRole CreateDefaultECSExpressExecutionRole()
    {
        return new Role(EnvironmentResource.CDKStack, "DefaultECSExpressExecutionRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryReadOnly"),
                ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"),
            }
        });
    }
    
    private IRole? _defaultECSExpressInfrastructureRole;
    public IRole GetDefaultECSExpressInfrastructureRole()
    {
        if (_defaultECSExpressInfrastructureRole == null)
        {            
            var definedDefault = FindDefaultConstructByAttribute<DefaultECSExpressInfrastructureRoleAttribute, IRole>();
            if (definedDefault != null)
            {
                _defaultECSExpressInfrastructureRole = definedDefault;
            }
            else
            {
                _defaultECSExpressInfrastructureRole = CreateDefaultECSExpressInfrastructureRole();
            }
        }

        return _defaultECSExpressInfrastructureRole;
    }    

    protected virtual IRole CreateDefaultECSExpressInfrastructureRole()
    {
        return new Role(EnvironmentResource.CDKStack, "DefaultECSExpressInfrastructureRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonECSInfrastructureRoleforExpressGatewayServices"),
            }
        });
    }

    private TConstruct? FindDefaultConstructByAttribute<TAttribute, TConstruct>()
        where TAttribute : Attribute
        where TConstruct : class
    {
        var properties = EnvironmentResource.CDKStack.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var prop in properties)
        {
            if (Attribute.IsDefined(prop, typeof(TAttribute)))
            {
                var value = prop.GetValue(EnvironmentResource.CDKStack);
                if (value == null)
                {
                    return null;
                }

                if (value is not TConstruct construct)
                {
                    throw new InvalidOperationException($"Property '{prop.Name}' is marked with '{typeof(TAttribute).Name}' but is not of type '{typeof(TConstruct).Name}'.");
                }

                return construct;
            }
        }

        var fields = EnvironmentResource.CDKStack.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var field in fields)
        {
            if (Attribute.IsDefined(field, typeof(TAttribute)))
            {
                var value = field.GetValue(EnvironmentResource.CDKStack);
                if (value == null)
                {
                    return null;
                }

                if (value is not TConstruct construct)
                {
                    throw new InvalidOperationException($"Field '{field.Name}' is marked with '{typeof(TAttribute).Name}' but is not of type '{typeof(TConstruct).Name}'.");
                }

                return construct;
            }
        }

        return null;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultVpcAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSClusterAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSClusterSecurityGroupAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSExpressExecutionRoleAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSExpressInfrastructureRoleAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheCfnSubnetGroupAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheCfnParameterGroupAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheSecurityGroupAttribute : Attribute
{

}