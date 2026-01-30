// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishElasticCacheProvisionClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishElastiCacheProvisionClusterConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing an ElastiCache Provisioned Cluster
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishElastiCacheProvisionClusterConfig
{
    /// <summary>
    /// When setting up connection strings for reference resources either the ConfigurationEndPoint
    /// or PrimaryEndPoint
    /// <see href="https://docs.aws.amazon.com/AWSCloudFormation/latest/TemplateReference/aws-resource-elasticache-replicationgroup.html#aws-resource-elasticache-replicationgroup-return-values-fn--getatt">
    /// CloudFormation return values</see> must be used depending on whether the
    /// cluster is configured for cluster mode or not. It is not possible from the CDK construct 
    /// to definitively determine whether cluster mode is enabled. When publishing to an ElastiCache
    /// cluster mode is assumed. If cluster mode is not enabled then set this property to false.
    /// </summary>
    /// <remarks>
    /// If property is null then cluster mode is assumed.
    /// </remarks>
    public bool? AssumeConnectionStringClusterMode { get; set; }

    /// <summary>
    /// Callback to modify the properties used to construct the CfnReplicationGroup
    /// </summary>
    public PublishCallback<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed CfnReplicationGroup
    /// </summary>
    public PublishCallback<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
}
