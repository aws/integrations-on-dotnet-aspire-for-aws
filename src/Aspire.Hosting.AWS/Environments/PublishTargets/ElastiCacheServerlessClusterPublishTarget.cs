// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments
{
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
}

namespace Aspire.Hosting
{
    public static partial class AWSCDKEnvironmentExtensions
    {
        [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
        public static IResourceBuilder<RedisResource> PublishAsElasticCacheServerlessCluster(this IResourceBuilder<RedisResource> builder, PublishCDKElastiCacheServerlessClusterConfig? config = null)
        {
            var annotation = new PublishCDKElasticCacheServerlessClusterAnnotation { Config = config ?? new PublishCDKElastiCacheServerlessClusterConfig() };
            builder.Resource.Annotations.Add(annotation);

            return builder;
        }
    }
}

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class ElastiCacheServerlessClusterPublishTarget(ILogger<ElastiCacheServerlessClusterPublishTarget> logger) : AbstractAWSPublishTarget(logger)
    {
        public override string PublishTargetName => "ElastiCache Serverless Cluster";

        public override Type PublishTargetAnnotation => typeof(PublishCDKElasticCacheServerlessClusterAnnotation);

        public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
        {
            var publishAnnotation = annotation as PublishCDKElasticCacheServerlessClusterAnnotation
                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKElasticCacheServerlessClusterAnnotation)}.");

            var serverlessCacheProps = new CfnServerlessCacheProps();

            //// Apply custom configuration
            publishAnnotation.Config.PropsCfnServerlessCacheCallback?.Invoke(serverlessCacheProps);

            // Apply defaults from provider
            environment.DefaultValuesProvider.ApplyCfnServerlessCachePropsDefaults(environment, serverlessCacheProps);

            var cluster = new CfnServerlessCache(environment.CDKStack, $"ElastiCache-{resource.Name}", serverlessCacheProps);

            // Apply construct-level customizations
            publishAnnotation.Config.ConstructCfnServerlessCacheCallback?.Invoke(cluster);

            ApplyLinkedConstructAnnotation(resource, cluster, this);

            return Task.CompletedTask;
        }

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
        {
            if (resource is RedisResource &&
                defaultProvider.DefaultRedisPublishTarget == DefaultProvider.RedisPublishTarget.ElastiCacheServerlessCluster
                )
            {
                return new IsDefaultPublishTargetMatchResult
                {
                    IsMatch = true,
                    PublishTargetAnnotation = new PublishCDKElasticCacheServerlessClusterAnnotation()
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
}