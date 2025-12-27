// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ElastiCacheServerlessClusterPublishTarget(ILogger<ElastiCacheServerlessClusterPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ElastiCache Serverless Cluster";

    public override Type PublishTargetAnnotation => typeof(PublishElasticCacheServerlessClusterAnnotation);

    public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishElasticCacheServerlessClusterAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishElasticCacheServerlessClusterAnnotation)}.");

        var serverlessCacheProps = new CfnServerlessCacheProps();

        //// Apply custom configuration
        publishAnnotation.Config.PropsCfnServerlessCacheCallback?.Invoke(serverlessCacheProps);

        // Apply defaults from provider
        environment.DefaultsProvider.ApplyCfnServerlessCachePropsDefaults(serverlessCacheProps);

        var cluster = new CfnServerlessCache(environment.CDKStack, $"ElastiCache-{resource.Name}", serverlessCacheProps);

        // Apply construct-level customizations
        publishAnnotation.Config.ConstructCfnServerlessCacheCallback?.Invoke(cluster);

        ApplyLinkedConstructAnnotation(resource, cluster, this);

        return Task.CompletedTask;
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is RedisResource &&
            cdkDefaultsProvider.DefaultRedisResourcePublishTarget == CDKDefaultsProvider.RedisResourcePublishTarget.ElastiCacheServerlessCluster
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishElasticCacheServerlessClusterAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
    {
        if (resourceConstruct is not CfnServerlessCache cacheConstruct)
            return null;

        var list = new List<KeyValuePair<string, string>>();

        var key = $"ConnectionStrings__{resource.Name}";
        var endpoint = $"{Token.AsString(cacheConstruct.AttrEndpointAddress)}:{Token.AsString(cacheConstruct.AttrEndpointPort)}";
        list.Add(new KeyValuePair<string, string>(key, endpoint));

        return list.Any() ? list : null;
    }
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishElastiCacheServerlessClusterConfig
{
    public Action<CfnServerlessCacheProps>? PropsCfnServerlessCacheCallback { get; set; }

    public Action<CfnServerlessCache>? ConstructCfnServerlessCacheCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishElasticCacheServerlessClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishElastiCacheServerlessClusterConfig Config { get; init; } = new PublishElastiCacheServerlessClusterConfig();
}