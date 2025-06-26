// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKElasticCacheRedisConfig
{
    public enum EngineType { Redis, Valkey }

    public required EngineType Engine { get; init; }

    public required string EngineVersion { get; init; }

    public required string CacheNodeType { get; init; }

    public required string CacheSubnetGroupName { get; init; }

    public required string[] SecurityGroupIds { get; init; }

    public required string CacheParameterGroupName { get; init; }

    public Action<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

    public Action<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKElasticCacheRedisAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public required PublishCDKElasticCacheRedisConfig Config { get; init; }
}