// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKElastiCacheServerlessClusterConfig
{
    public Action<CfnServerlessCacheProps>? PropsCfnServerlessCacheCallback { get; set; }

    public Action<CfnServerlessCache>? ConstructCfnServerlessCacheCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKElasticCacheServerlessClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishCDKElastiCacheServerlessClusterConfig Config { get; init; } = new PublishCDKElastiCacheServerlessClusterConfig();
}