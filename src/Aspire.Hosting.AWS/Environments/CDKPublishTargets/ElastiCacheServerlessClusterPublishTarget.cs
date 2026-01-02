// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;
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
        environment.DefaultsProvider.ApplyCfnServerlessCachePropsDefaults(serverlessCacheProps, resource);

        var cluster = new CfnServerlessCache(environment.CDKStack, $"ElastiCache-{resource.Name}", serverlessCacheProps);

        // Apply construct-level customizations
        publishAnnotation.Config.ConstructCfnServerlessCacheCallback?.Invoke(cluster);

        ApplyAWSLinkedObjectsAnnotation(environment, resource, cluster, this);

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

    public override GetReferencesResult GetReferences(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new GetReferencesResult();
        if (linkedAnnotation.Construct is not CfnServerlessCache cacheConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        var key = $"ConnectionStrings__{linkedAnnotation.Resource.Name}";
        var endpoint = $"{Token.AsString(cacheConstruct.AttrEndpointAddress)}:{Token.AsString(cacheConstruct.AttrEndpointPort)},ssl=True";
        result.EnvironmentVariables[key] = endpoint;
        
        return result;
    }

    public override bool ReferenceRequiresVPC()
    {
        return true;
    }

    public override bool ReferenceRequiresSecurityGroup()
    {
        return true;
    }

    public override void ApplyReferenceSecurityGroup(AWSLinkedObjectsAnnotation linkedAnnotation, ISecurityGroup securityGroup)
    {
        var elastiCacheSecurityGroup = linkedAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultElastiCacheServerlessClusterSecurityGroup();
        elastiCacheSecurityGroup.AddIngressRule(peer: securityGroup, connection: Port.Tcp(6379));
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
    public PublishElastiCacheServerlessClusterConfig Config { get; set; } = new ();
}