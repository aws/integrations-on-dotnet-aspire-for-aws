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
internal class ElastiCacheNodeClusterPublishTarget(ILogger<ElastiCacheNodeClusterPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ElastiCache Node Cluster";

    public override Type PublishTargetAnnotation => typeof(PublishElasticCacheNodeClusterAnnotation);

    public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishElasticCacheNodeClusterAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishElasticCacheNodeClusterAnnotation)}.");

        var clusterProps = new CfnReplicationGroupProps();
        publishAnnotation.Config.PropsCfnReplicationGroupCallback?.Invoke(clusterProps);
        environment.DefaultsProvider.ApplyCfnReplicationGroupPropsDefaults(clusterProps);

        var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
        publishAnnotation.Config.ConstructCfnReplicationGroupCallback?.Invoke(cluster);
        ApplyAWSLinkedObjectsAnnotation(environment, resource, cluster, this);
            
        return Task.CompletedTask;
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is RedisResource &&
            cdkDefaultsProvider.DefaultRedisResourcePublishTarget == CDKDefaultsProvider.RedisResourcePublishTarget.ElastiCacheNodeCluster
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishElasticCacheNodeClusterAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override GetReferencesResult GetReferences(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new GetReferencesResult();
        if (linkedAnnotation.Construct is not CfnReplicationGroup cacheConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        var key = $"ConnectionStrings__{linkedAnnotation.Resource.Name}";
        var endpoint = $"{Token.AsString(cacheConstruct.AttrPrimaryEndPointAddress)}:{Token.AsString(cacheConstruct.AttrPrimaryEndPointPort)}";
        result.EnvironmentVariables[key] = endpoint;

        return result;
    }
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishElastiCacheNodeClusterConfig
{
    public Action<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

    public Action<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishElasticCacheNodeClusterAnnotation : IAWSPublishTargetAnnotation
{
    public PublishElastiCacheNodeClusterConfig Config { get; set; } = new ();
}