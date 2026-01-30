// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElastiCache;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ElastiCacheProvisionClusterPublishTarget(ILogger<ElastiCacheProvisionClusterPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ElastiCache Provision Cluster";

    public override Type PublishTargetAnnotation => typeof(PublishElasticCacheProvisionClusterAnnotation);

    public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishElasticCacheProvisionClusterAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishElasticCacheProvisionClusterAnnotation)}.");

        var clusterProps = new CfnReplicationGroupProps();
        publishAnnotation.Config.PropsCfnReplicationGroupCallback?.Invoke(CreatePublishTargetContext(environment), clusterProps);
        environment.DefaultsProvider.ApplyCfnReplicationGroupPropsDefaults(clusterProps);

        var cluster = new CfnReplicationGroup(environment.CDKStack, $"ElastiCache-{resource.Name}", clusterProps);
        publishAnnotation.Config.ConstructCfnReplicationGroupCallback?.Invoke(CreatePublishTargetContext(environment), cluster);
        ApplyAWSLinkedObjectsAnnotation(environment, resource, cluster, this);

        return Task.CompletedTask;
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if ((resource is RedisResource || resource is ValkeyResource) &&
            cdkDefaultsProvider.DefaultRedisResourcePublishTarget == CDKDefaultsProvider.RedisResourcePublishTarget.ElastiCacheProvisionCluster
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishElasticCacheProvisionClusterAnnotation()
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not CfnReplicationGroup cacheConstruct)
            return result;

        if (!linkedAnnotation.Resource.TryGetLastAnnotation<PublishElasticCacheProvisionClusterAnnotation>(out var publishAnnotation))
            throw new InvalidDataException($"Missing {nameof(PublishElasticCacheProvisionClusterAnnotation)} for resource {linkedAnnotation.Resource.Name}.");

        result.EnvironmentVariables = new Dictionary<string, string>();

        var key = $"ConnectionStrings__{linkedAnnotation.Resource.Name}";

        string? endpoint;
        if (publishAnnotation.Config.AssumeConnectionStringClusterMode == null || publishAnnotation.Config.AssumeConnectionStringClusterMode == true)
        {
            endpoint = $"{Token.AsString(cacheConstruct.AttrConfigurationEndPointAddress)}:{Token.AsString(cacheConstruct.AttrConfigurationEndPointPort)}";

            // Log a message to the user since we are making an assumption that the user might need to change.
            if (publishAnnotation.Config.AssumeConnectionStringClusterMode == null)
                logger.LogInformation("Generating connection string for resource {Resource} assuming cluster mode is enabled. If an error during deployment happens about attributes not found the {Property} property on {Config} might need to be set to false.", linkedAnnotation.Resource.Name, nameof(PublishElastiCacheProvisionClusterConfig.AssumeConnectionStringClusterMode), nameof(PublishElastiCacheProvisionClusterConfig));
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
        var elastiCacheSecurityGroup = linkedAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultElastiCacheProvisionClusterSecurityGroup();
        elastiCacheSecurityGroup.AddIngressRule(peer: securityGroup, connection: Port.Tcp(6379));
        elastiCacheSecurityGroup.AddIngressRule(peer: securityGroup, connection: Port.Tcp(6380));
    }
}
