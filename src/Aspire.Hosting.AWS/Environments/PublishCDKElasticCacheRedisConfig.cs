// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;

namespace Aspire.Hosting.AWS.Environments;

public class PublishCDKElasticCacheRedisConfig
{
    public enum EngineType { Redis, Valkey }

    public required EngineType Engine { get; init; }

    public required string EngineVersion { get; init; }

    public required string CacheNodeType { get; init; }

    public required string CacheSubnetGroupName { get; init; }

    public required string[] SecurityGroupIds { get; init; }

    public required string CacheParameterGroupName { get; init; }

    public Action<CfnReplicationGroupProps>? PropsCallback { get; set; }

    public Action<CfnReplicationGroup>? ConstructCallback { get; set; }
}

internal class PublishCDKElasticCacheRedisAnnotation : Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    public required PublishCDKElasticCacheRedisConfig Config { get; init; }
}