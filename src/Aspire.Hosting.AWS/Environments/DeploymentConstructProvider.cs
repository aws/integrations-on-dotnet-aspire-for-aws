using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElastiCache;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class DeploymentConstructProvider
{
    public AWSCDKEnvironmentResource Environment { get; }


    internal DeploymentConstructProvider(AWSCDKEnvironmentResource environment)
    {
        Environment = environment;
    }

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
                _defaultVpc = Environment.DefaultValuesProvider.CreateDefaultVpc(Environment);
            }
        }

        return _defaultVpc;
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
                _defaultECSCluster = Environment.DefaultValuesProvider.CreateDefaultECSCluster(Environment);
            }
        }

        return _defaultECSCluster;
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
                _defaultECSClusterSecurityGroup = Environment.DefaultValuesProvider.CreateDefaultECSClusterSecurityGroup(Environment);
            }
        }
        return _defaultECSClusterSecurityGroup;
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
                _defaultElastiCacheCfnSubnetGroup = Environment.DefaultValuesProvider.CreateDefaultElastiCacheCfnSubnetGroup(Environment);
            }
        }
        return _defaultElastiCacheCfnSubnetGroup;
    }

    public CfnParameterGroup? _defaultElastiCacheCfnParameterGroup;
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
                _defaultElastiCacheCfnParameterGroup = Environment.DefaultValuesProvider.CreateDefaultElastiCacheCfnParameterGroup(Environment);
            }
        }

        return _defaultElastiCacheCfnParameterGroup;
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
                _defaultElastiCacheSecurityGroup = Environment.DefaultValuesProvider.CreateDefaultElastiCacheSecurityGroup(Environment);
            }
        }

        return _defaultElastiCacheSecurityGroup;
    }

    private TConstruct? FindDefaultConstructByAttribute<TAttribute, TConstruct>()
        where TAttribute : Attribute
        where TConstruct : class
    {
        var properties = Environment.CDKStack.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var prop in properties)
        {
            if (Attribute.IsDefined(prop, typeof(TAttribute)))
            {
                var value = prop.GetValue(Environment.CDKStack);
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

        var fields = Environment.CDKStack.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var field in fields)
        {
            if (Attribute.IsDefined(field, typeof(TAttribute)))
            {
                var value = field.GetValue(Environment.CDKStack);
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