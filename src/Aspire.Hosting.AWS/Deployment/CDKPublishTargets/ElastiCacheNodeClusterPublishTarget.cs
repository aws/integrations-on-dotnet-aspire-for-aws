// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets
{
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
            if ((resource is RedisResource || resource is ValkeyResource) &&
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

            if (!linkedAnnotation.Resource.TryGetLastAnnotation<PublishElasticCacheNodeClusterAnnotation>(out var publishAnnotation))
                throw new InvalidDataException($"Missing PublishElasticCacheNodeClusterAnnotation for resource {linkedAnnotation.Resource.Name}.");

            result.EnvironmentVariables = new Dictionary<string, string>();

            var key = $"ConnectionStrings__{linkedAnnotation.Resource.Name}";

            string? endpoint;
            if (publishAnnotation.Config.AssumeConnectionStringClusterMode == null || publishAnnotation.Config.AssumeConnectionStringClusterMode == true)
            {
                endpoint = $"{Token.AsString(cacheConstruct.AttrConfigurationEndPointAddress)}:{Token.AsString(cacheConstruct.AttrConfigurationEndPointPort)}";

                // Log a message to the user since we are making an assumption that the user might need to change.
                if (publishAnnotation.Config.AssumeConnectionStringClusterMode == null)
                    logger.LogInformation("Generating connection string for resource {Resource} assuming cluster mode is enabled. If an error during deployment happens about attributes not found the {Property} property on {Config} might need to be set to false.", linkedAnnotation.Resource.Name, nameof(PublishElastiCacheNodeClusterConfig.AssumeConnectionStringClusterMode), nameof(PublishElastiCacheNodeClusterConfig));
            }
            else
            {
                endpoint = $"{Token.AsString(cacheConstruct.AttrPrimaryEndPointAddress)}:{Token.AsString(cacheConstruct.AttrPrimaryEndPointPort)}";
            }

            if (string.Equals(cacheConstruct.TransitEncryptionEnabled?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                endpoint += ",ssl=True";

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
            var elastiCacheSecurityGroup = linkedAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultElastiCacheNodeClusterSecurityGroup();
            elastiCacheSecurityGroup.AddIngressRule(peer: securityGroup, connection: Port.Tcp(6379));
        }
    }
}

namespace Aspire.Hosting.AWS.Deployment
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public class PublishElastiCacheNodeClusterConfig
    {
        /// <summary>
        /// When setting up connection strings for reference resources either the ConfigurationEndPoint
        /// or PrimaryEndPoint
        /// <see cref="https://docs.aws.amazon.com/AWSCloudFormation/latest/TemplateReference/aws-resource-elasticache-replicationgroup.html#aws-resource-elasticache-replicationgroup-return-values-fn--getatt">
        /// CloudFormation return values</see> must be used depending on whether the
        /// cluster is configured for cluster mode or not. It is not possible from the CDK construct 
        /// to definitively determine whether cluster mode is enabled. When publishing to an ElastiCache
        /// cluster mode is assumed. If cluster mode is not enabled then set this property to false.
        /// </summary>
        /// <remarks>
        /// If property is null then cluster mode is assumed.
        /// </remarks>
        public bool? AssumeConnectionStringClusterMode { get; set; }

        public Action<CfnReplicationGroupProps>? PropsCfnReplicationGroupCallback { get; set; }

        public Action<CfnReplicationGroup>? ConstructCfnReplicationGroupCallback { get; set; }
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class PublishElasticCacheNodeClusterAnnotation : IAWSPublishTargetAnnotation
    {
        public PublishElastiCacheNodeClusterConfig Config { get; set; } = new();
    }
}
