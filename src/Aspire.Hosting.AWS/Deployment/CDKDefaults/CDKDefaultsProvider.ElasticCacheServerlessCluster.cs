// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

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
            var subnets = GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId);
            if (!subnets.Any())
                subnets = GetDefaultVpc().PublicSubnets.Select(s => s.SubnetId);

            props.SubnetIds = subnets.Take(2).ToArray();
        }

        if (props.SecurityGroupIds == null)
        {
            props.SecurityGroupIds = new[] { GetDefaultElastiCacheServerlessClusterSecurityGroup().SecurityGroupId };
        }
        else
        {
            // Even if the user set the SecurityGroupIds still append the default security group which will be used 
            // when adding permissions for Aspire references.
            var securityGroupsxistingSecurityGroup = new List<object>(props.SecurityGroupIds);
            securityGroupsxistingSecurityGroup.Add(GetDefaultElastiCacheServerlessClusterSecurityGroup().SecurityGroupId);
            props.SecurityGroupIds = securityGroupsxistingSecurityGroup.ToArray();
        }
    }    
}
