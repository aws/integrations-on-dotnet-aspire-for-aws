// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using static Aspire.Hosting.AWS.Environments.CDKResourceContexts.IAWSPublishTarget;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ElastiCacheNodeClusterPublishTarget(ILogger<ElastiCacheNodeClusterPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ElastiCache Node Cluster";

    public override Type PublishTargetAnnotation => typeof(PublishCDKElasticCacheNodeClusterAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishCDKElasticCacheNodeClusterAnnotation
            ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKElasticCacheNodeClusterAnnotation)}.");

        var clusterProps = new CfnReplicationGroupProps();
        publishAnnotation.Config.PropsCfnReplicationGroupCallback?.Invoke(clusterProps);
        environment.DefaultValuesProvider.ApplyCfnReplicationGroupPropsDefaults(environment, clusterProps);

        var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
        publishAnnotation.Config.ConstructCfnReplicationGroupCallback?.Invoke(cluster);
        ApplyLinkedConstructAnnotation(resource, cluster, this);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
    {
        if (resource is RedisResource &&
            defaultProvider.DefaultRedisPublishTarget == DefaultProvider.RedisPublishTarget.ElastiCacheNodeCluster
            )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishCDKElasticCacheNodeClusterAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
    {
        if (resourceConstruct is not CfnReplicationGroup cacheConstruct)
            return null;

        var list = new List<KeyValuePair<string, string>>();

        var key = $"ConnectionStrings__{resource.Name}";
        var endpoint = $"{Token.AsString(cacheConstruct.AttrPrimaryEndPointAddress)}:{Token.AsString(cacheConstruct.AttrPrimaryEndPointPort)}";
        list.Add(new KeyValuePair<string, string>(key, endpoint));

        return list.Any() ? list : null;
    }
}
