// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Environments.CDKDefaults;

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

    public virtual string ElasticCacheNodeClusterParameterGroupFamily => "valkey8";

    public virtual string ElasticCacheNodeClusterParameterGroupDescription => "Parameter group for Node Cluster";

    public virtual bool? ElasticCacheNodeClusterTransitEncryptionEnabled => false;

    public virtual IDictionary<string, string>? ElasticCacheNodeClusterParameterGroupProperties => new Dictionary<string, string>
    {
    };

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

        if (props.CacheSubnetGroupName == null)
            props.CacheSubnetGroupName = GetDefaultElastiCacheCfnSubnetGroup().Ref;
        if (props.CacheParameterGroupName == null) // TODO: Figure out of this hardcoded is okay
            props.CacheParameterGroupName = "default.valkey8.cluster.on";// environment.DeploymentConstructProvider.GetDefaultElastiCacheCfnParameterGroup().Ref;
        if (props.SecurityGroupIds == null)
            props.SecurityGroupIds = new[] { GetDefaultElastiCacheSecurityGroup().SecurityGroupId };
    }    
}
