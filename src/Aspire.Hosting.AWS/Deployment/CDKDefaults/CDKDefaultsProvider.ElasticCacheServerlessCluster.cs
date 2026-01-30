// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the name of the engine used for Elastic Cache Serverless clusters.
    /// </summary>
    /// <remarks>
    /// Default is "valkey".
    /// </remarks>
    public virtual string ElasticCacheServerlessClusterEngine => "valkey";

    /// <summary>
    /// Gets the major engine version used for ElastiCache Serverless.
    /// </summary>
    /// <remarks>
    /// Default is "8".
    /// </remarks>
    public virtual string ElasticCacheServerlessMajorEngineVersion => "8";

    /// <summary>
    /// Apply default values to the specified properties for configuring an AWS CloudFormation ElastiCache Serverless Cache,
    /// </summary>
    /// <remarks>
    /// The default ElastiCache Serverless Cache security group will always be added to the SecurityGroupIds property to allow
    /// security group to security group ingress rules can be created for Aspire references.
    /// </remarks>
    /// <param name="props">>The <see cref="CfnServerlessCacheProps"/> object to which default values will be applied. Properties that are
    /// null will be set to their default values.</param>
    /// <param name="resource">The Aspire resource for which the name will be used for creating default values.</param>
    protected internal virtual void ApplyCfnServerlessCachePropsDefaults(CfnServerlessCacheProps props, ApplicationModel.IResource resource)
    {
        if (props.ServerlessCacheName == null)
            props.ServerlessCacheName = $"{this.EnvironmentResource.CDKStack.StackName}-{resource.Name}";
        if (props.Engine == null)
            props.Engine = ElasticCacheServerlessClusterEngine;
        if (props.MajorEngineVersion == null)
            props.MajorEngineVersion = ElasticCacheServerlessMajorEngineVersion;

        if (props.SubnetIds == null)
        {
            var subnetIds = GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId);
            if (!subnetIds.Any())
                subnetIds = GetDefaultVpc().PublicSubnets.Select(s => s.SubnetId);

            props.SubnetIds = subnetIds.Take(2).ToArray();
        }

        if (props.SecurityGroupIds == null)
        {
            props.SecurityGroupIds = new[] { GetDefaultElastiCacheServerlessClusterSecurityGroup().SecurityGroupId };
        }
        else
        {
            // Even if the user set the SecurityGroupIds still append the default security group which will be used 
            // when adding permissions for Aspire references.
            var securityGroupsExistingSecurityGroup = new List<object>(props.SecurityGroupIds)
            {
                GetDefaultElastiCacheServerlessClusterSecurityGroup().SecurityGroupId
            };
            props.SecurityGroupIds = securityGroupsExistingSecurityGroup.ToArray();
        }
    }    
}
