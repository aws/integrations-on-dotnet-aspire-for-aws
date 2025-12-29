// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Environments.CDKDefaults;

public partial class CDKDefaultsProvider
{
    public virtual string ElasticCacheServerlessClusterEngine => "valkey";

    public virtual string ElasticCacheServerlessMajorEngineVersion => "8";

    protected internal virtual void ApplyCfnServerlessCachePropsDefaults(CfnServerlessCacheProps props, Aspire.Hosting.ApplicationModel.IResource resource)
    {
        if (props.ServerlessCacheName == null)
            props.ServerlessCacheName = $"{this.EnvironmentResource.CDKStack.StackName}-{resource.Name}";
        if (props.Engine == null)
            props.Engine = ElasticCacheServerlessClusterEngine;
        if (props.MajorEngineVersion == null)
            props.MajorEngineVersion = ElasticCacheServerlessMajorEngineVersion;

        if (props.SubnetIds == null)
        {
            props.SubnetIds = GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId).ToArray();
        }

        if (props.SecurityGroupIds == null)
        {
            if (props.SecurityGroupIds == null)
                props.SecurityGroupIds = new[] { GetDefaultElastiCacheSecurityGroup().SecurityGroupId };
        }
    }    
}
