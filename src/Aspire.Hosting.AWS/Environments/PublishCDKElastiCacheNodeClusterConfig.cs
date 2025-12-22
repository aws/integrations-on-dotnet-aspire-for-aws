// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKElastiCacheNodeClusterConfig
{
    public Action<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

    public Action<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKElasticCacheNodeClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishCDKElastiCacheNodeClusterConfig Config { get; init; } = new PublishCDKElastiCacheNodeClusterConfig();
}