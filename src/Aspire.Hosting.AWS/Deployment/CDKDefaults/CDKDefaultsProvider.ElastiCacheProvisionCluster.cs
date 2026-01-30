// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the description for the Elastic Cache cluster replication group provisioned for the Aspire application.
    /// </summary>
    /// <remarks>
    /// Default is "Provision Cache for Aspire Application".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterReplicationGroupDescription => "Provision Cache for Aspire Application";

    /// <summary>
    /// Gets the name of the cache engine used by the provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is "valkey".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterEngine => "valkey";

    /// <summary>
    /// Gets the default engine version used for ElasticCache provision clusters.
    /// </summary>
    /// <remarks>
    /// Default is "8.2".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterEngineVersion => "8.2";

    /// <summary>
    /// Gets the cache node type used for the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is "cache.t3.micro".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterCacheNodeType => "cache.t3.micro";

    /// <summary>
    /// Gets the number of cache clusters in the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is 2.
    /// </remarks>
    public virtual double ElasticCacheProvisionClusterNumCacheClusters => 2;

    /// <summary>
    /// Gets a value indicating whether automatic failover is enabled for the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is true.
    /// </remarks>
    public virtual bool ElasticCacheProvisionClusterAutomaticFailoverEnabled => true;

    /// <summary>
    /// Gets the default port number used for connecting to an Amazon ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is 6379.
    /// </remarks>
    public virtual double ElasticCacheProvisionClusterPort => 6379;

    /// <summary>
    /// Gets the description of the subnet group used for the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is "Subnet group for ElastiCache provision cluster".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterSubnetGroupDescription => "Subnet group for ElastiCache Provision Cluster";

    /// <summary>
    /// Gets the value indicating whether transit encryption is enabled for the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is true.
    /// </remarks>
    public virtual bool? ElasticCacheProvisionClusterTransitEncryptionEnabled => true;

    /// <summary>
    /// Gets a value indicating whether at-rest encryption is enabled for the ElastiCache provision cluster.
    /// </summary>
    /// <remarks>
    /// Default is true.
    /// </remarks>
    public virtual bool? ElasticCacheProvisionClusterAtRestEncryptionEnabled => true;

    /// <summary>
    /// Gets the name of the default cache parameter group for ElastiCache provision clusters using Valkey 8 in cluster mode.
    /// </summary>
    /// <remarks>
    /// Default is "default.valkey8.cluster.on".
    /// </remarks>
    public virtual string ElasticCacheProvisionClusterCacheParameterGroupName => "default.valkey8.cluster.on";

    /// <summary>
    /// Applies default values to any unset properties of the specified <see cref="CfnReplicationGroupProps"/> instance.
    /// </summary>
    /// <param name="props">The <see cref="CfnReplicationGroupProps"/> object to which default values will be applied. Properties that are
    /// null will be set to their default values.</param>
    protected internal virtual void ApplyCfnReplicationGroupPropsDefaults(CfnReplicationGroupProps props)
    {
        if (props.ReplicationGroupDescription == null)
            props.ReplicationGroupDescription = ElasticCacheProvisionClusterReplicationGroupDescription;
        if (props.CacheNodeType == null)
            props.CacheNodeType = ElasticCacheProvisionClusterCacheNodeType;
        if (props.Engine == null)
            props.Engine = ElasticCacheProvisionClusterEngine;
        if (props.EngineVersion == null)
            props.EngineVersion = ElasticCacheProvisionClusterEngineVersion;
        if (props.NumCacheClusters == null)
            props.NumCacheClusters = ElasticCacheProvisionClusterNumCacheClusters;
        if (props.AutomaticFailoverEnabled == null)
            props.AutomaticFailoverEnabled = ElasticCacheProvisionClusterAutomaticFailoverEnabled;
        if (props.Port == null)
            props.Port = ElasticCacheProvisionClusterPort;
        if (props.TransitEncryptionEnabled == null)
            props.TransitEncryptionEnabled = ElasticCacheProvisionClusterTransitEncryptionEnabled;
        if (props.AtRestEncryptionEnabled == null)
            props.AtRestEncryptionEnabled = ElasticCacheProvisionClusterAtRestEncryptionEnabled;        

        if (props.CacheSubnetGroupName == null)
            props.CacheSubnetGroupName = GetDefaultElastiCacheCfnSubnetGroup().Ref;
        if (props.CacheParameterGroupName == null)
            props.CacheParameterGroupName = ElasticCacheProvisionClusterCacheParameterGroupName;
        if (props.SecurityGroupIds == null)
            props.SecurityGroupIds = new[] { GetDefaultElastiCacheProvisionClusterSecurityGroup().SecurityGroupId };
    }    
}
