// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ElastiCache;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishElasticCacheServerlessClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishElastiCacheServerlessClusterConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing an ElastiCache Serverless Cluster
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishElastiCacheServerlessClusterConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the CfnServerlessCache
    /// </summary>
    public PublishCallback<CfnServerlessCacheProps>? PropsCfnServerlessCacheCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed CfnServerlessCache
    /// </summary>
    public PublishCallback<CfnServerlessCache>? ConstructCfnServerlessCacheCallback { get; set; }
}
