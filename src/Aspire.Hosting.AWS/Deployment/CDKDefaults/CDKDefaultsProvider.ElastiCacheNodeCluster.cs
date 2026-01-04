// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    public virtual string ElasticCacheNodeClusterReplicationGroupDescription => "Node Cache for Aspire Application";

    public virtual string ElasticCacheNodeClusterEngine => "valkey";

    public virtual string? ElasticCacheNodeClusterEngineVersion => "8.2";

    public virtual string? ElasticCacheNodeClusterCacheNodeType => "cache.t3.micro";

    public virtual double? ElasticCacheNodeClusterNumCacheClusters => 2;

    public virtual bool? ElasticCacheNodeClusterAutomaticFailoverEnabled => true;

    public virtual double ElasticCacheNodeClusterPort => 6379;

    public virtual string ElasticCacheNodeClusterSubnetGroupDescription => "Subnet group for ElastiCache Node Cluster";

    public virtual bool? ElasticCacheNodeClusterTransitEncryptionEnabled => true;

    public virtual bool? ElasticCacheNodeClusterAtRestEncryptionEnabled => true;

    public virtual string ElasticCacheNodeClusterCacheParameterGroupName => "default.valkey8.cluster.on";

    protected internal virtual void ApplyCfnReplicationGroupPropsDefaults(CfnReplicationGroupProps props)
    {
        if (props.ReplicationGroupDescription == null)
            props.ReplicationGroupDescription = ElasticCacheNodeClusterReplicationGroupDescription;
        if (props.CacheNodeType == null)
            props.CacheNodeType = ElasticCacheNodeClusterCacheNodeType;
        if (props.Engine == null)
            props.Engine = ElasticCacheNodeClusterEngine;
        if (props.EngineVersion == null)
            props.EngineVersion = ElasticCacheNodeClusterEngineVersion;
        if (props.NumCacheClusters == null)
            props.NumCacheClusters = ElasticCacheNodeClusterNumCacheClusters;
        if (props.AutomaticFailoverEnabled == null)
            props.AutomaticFailoverEnabled = ElasticCacheNodeClusterAutomaticFailoverEnabled;
        if (props.Port == null)
            props.Port = ElasticCacheNodeClusterPort;
        if (props.TransitEncryptionEnabled == null)
            props.TransitEncryptionEnabled = ElasticCacheNodeClusterTransitEncryptionEnabled;
        if (props.AtRestEncryptionEnabled == null)
            props.AtRestEncryptionEnabled = ElasticCacheNodeClusterAtRestEncryptionEnabled;        

        if (props.CacheSubnetGroupName == null)
            props.CacheSubnetGroupName = GetDefaultElastiCacheCfnSubnetGroup().Ref;
        if (props.CacheParameterGroupName == null)
            props.CacheParameterGroupName = ElasticCacheNodeClusterCacheParameterGroupName;
        if (props.SecurityGroupIds == null)
            props.SecurityGroupIds = new[] { GetDefaultElastiCacheNodeClusterSecurityGroup().SecurityGroupId };
    }    
}
