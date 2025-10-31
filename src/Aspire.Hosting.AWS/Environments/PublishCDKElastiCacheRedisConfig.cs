// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKElastiCacheRedisConfig
{
    public Action<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

    public Action<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKElasticCacheRedisAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public PublishCDKElastiCacheRedisConfig Config { get; init; } = new PublishCDKElastiCacheRedisConfig();
}