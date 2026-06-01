// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using IResource = Aspire.Hosting.ApplicationModel.IResource;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class S3StaticWebsitePublishTarget(
    IStaticSiteBuilder siteBuilder,
    ILogger<S3StaticWebsitePublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "S3 with CloudFront";

    public override Type PublishTargetAnnotation => typeof(PublishS3WithCloudFrontAnnotation);

    public override async Task GenerateConstructAsync(
        AWSCDKEnvironmentResource environment,
        IResource resource,
        IAWSPublishTargetAnnotation annotation,
        CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishS3WithCloudFrontAnnotation
            ?? throw new InvalidOperationException($"Annotation for resource '{resource.Name}' is not a valid {nameof(PublishS3WithCloudFrontAnnotation)}.");

        var config = publishAnnotation.Config;

        var workingDirectory = publishAnnotation.WorkingDirectory
            ?? throw new InvalidOperationException(
                $"Resource '{resource.Name}' is missing a working directory. " +
                $"Ensure PublishAsS3WithCloudFront() is called on a JavaScript resource.");

        var connectionPoints = new StaticSiteConnectionPoints();
        ProcessRelationShips(connectionPoints, resource, environment);
        var buildEnvVars = connectionPoints.EnvironmentVariables ?? new Dictionary<string, string>();

        await siteBuilder.BuildAsync(resource, workingDirectory, buildEnvVars, cancellationToken);

        if (Path.IsPathRooted(config.OutputPath))
            throw new InvalidOperationException(
                $"OutputPath must be a relative path, but got: '{config.OutputPath}'.");

        var buildOutputPath = Path.GetFullPath(Path.Combine(workingDirectory, config.OutputPath));

        var expectedRoot = workingDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!buildOutputPath.StartsWith(expectedRoot, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"OutputPath '{config.OutputPath}' resolves to '{buildOutputPath}', which is outside the working directory '{workingDirectory}'.");
        var context = CreatePublishTargetContext(environment);

        // --- S3 Bucket ---
        var bucketProps = new BucketProps();
        config.PropsBucketCallback?.Invoke(context, bucketProps);
        environment.DefaultsProvider.ApplyS3WithCloudFrontBucketDefaults(bucketProps);

        var bucket = new Bucket(environment.CDKStack, $"Project-{resource.Name}-Bucket", bucketProps);
        config.ConstructBucketCallback?.Invoke(context, bucket);

        // --- CloudFront distribution ---
        var behaviorOptions = new BehaviorOptions
        {
            Origin = S3BucketOrigin.WithOriginAccessControl(bucket),
        };

        var distributionProps = new DistributionProps
        {
            DefaultBehavior = behaviorOptions,
        };

        // Attach backend behaviors declared via WithCloudFrontBackendBehavior
        var backendBehaviorAnnotations = resource.Annotations.OfType<CloudFrontBehaviorAnnotation>().ToList();
        if (backendBehaviorAnnotations.Count > 0)
        {
            var additionalBehaviors = new Dictionary<string, IBehaviorOptions>();
            foreach (var behaviorAnnotation in backendBehaviorAnnotations)
            {
                if (!behaviorAnnotation.BackendResource.TryGetLastAnnotation<AWSLinkedObjectsAnnotation>(out var linkedAnnotation))
                    throw new InvalidOperationException(
                        $"Backend resource '{behaviorAnnotation.BackendResource.Name}' has not been published yet. " +
                        $"Ensure it is defined before the static website resource in your AppHost.");

                var (originHostname, protocolPolicy) = ResolveBackendHostname(linkedAnnotation, behaviorAnnotation.BackendResource.Name);

                var behavior = new BehaviorOptions
                {
                    Origin = new HttpOrigin(originHostname, new HttpOriginProps
                    {
                        ProtocolPolicy = protocolPolicy,
                    }),
                };
                environment.DefaultsProvider.ApplyS3WithCloudFrontBackendBehaviorDefaults(behavior);

                additionalBehaviors[behaviorAnnotation.PathPattern] = behavior;

                // CloudFront's "/foo/*" matches "/foo/bar" but NOT "/foo".
                // Register the bare path as a separate exact-match behavior.
                if (behaviorAnnotation.PathPattern.EndsWith("/*"))
                {
                    var basePath = behaviorAnnotation.PathPattern[..^2];
                    additionalBehaviors.TryAdd(basePath, behavior);
                }
            }
            distributionProps.AdditionalBehaviors = additionalBehaviors;
        }

        config.PropsDistributionCallback?.Invoke(context, distributionProps);
        environment.DefaultsProvider.ApplyS3WithCloudFrontDistributionDefaults(distributionProps);

        var distribution = new Distribution(environment.CDKStack, $"Project-{resource.Name}-Distribution", distributionProps);
        config.ConstructDistributionCallback?.Invoke(context, distribution);

        new CfnOutput(environment.CDKStack, $"{resource.Name}-CloudFrontUrl", new CfnOutputProps
        {
            Value = $"https://{distribution.DomainName}",
        });

        // --- Bucket deployment ---
        var deploymentProps = new BucketDeploymentProps
        {
            Sources = [Source.Asset(buildOutputPath)],
            DestinationBucket = bucket,
            Distribution = distribution,
            DistributionPaths = ["/*"],
        };
        config.PropsBucketDeploymentCallback?.Invoke(context, deploymentProps);
        new BucketDeployment(environment.CDKStack, $"Project-{resource.Name}-Deployment", deploymentProps);

        ApplyAWSLinkedObjectsAnnotation(environment, resource, distribution, this);
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not Distribution distribution)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>
        {
            [$"services__{linkedAnnotation.Resource.Name}__https__0"] =
                Fn.Join("", ["https://", distribution.DomainName, "/"]),
        };
        return result;
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is JavaScriptAppResource jsResource)
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishS3WithCloudFrontAnnotation { WorkingDirectory = jsResource.WorkingDirectory },
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK,
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    /// <summary>
    /// Resolves the backend origin hostname and protocol by calling <see cref="IAWSPublishTarget.GetReferenceConnectionInfo"/>
    /// on the linked resource and extracting the host from the returned service-discovery URL. Supports any
    /// publish target that exposes <c>services__{name}__https__0</c> or <c>services__{name}__http__0</c>.
    /// The protocol policy mirrors the scheme of the discovered URL.
    /// </summary>
    /// <remarks>
    /// Port handling: this method assumes the backend listens on the standard port for its scheme
    /// (80 for HTTP, 443 for HTTPS). Any port embedded in the service-discovery URL is stripped
    /// because the service-discovery values are CDK token strings at synthesis time — the port
    /// cannot be extracted and converted to the numeric <see cref="HttpOriginProps.HttpPort"/> /
    /// <see cref="HttpOriginProps.HttpsPort"/> CDK properties. If your backend uses a non-standard
    /// port, customize the origin via <see cref="PublishS3WithCloudFrontConfig.PropsDistributionCallback"/>.
    /// </remarks>
    private static (string Hostname, OriginProtocolPolicy Protocol) ResolveBackendHostname(AWSLinkedObjectsAnnotation linkedAnnotation, string resourceName)
    {
        var connectionInfo = linkedAnnotation.PublishTarget.GetReferenceConnectionInfo(linkedAnnotation);
        var envVars = connectionInfo.EnvironmentVariables ?? new Dictionary<string, string>();

        envVars.TryGetValue($"services__{resourceName}__https__0", out var httpsUrl);
        envVars.TryGetValue($"services__{resourceName}__http__0", out var httpUrl);
        string? originUrl = httpsUrl ?? httpUrl;

        if (originUrl == null)
            throw new InvalidOperationException(
                $"Backend resource '{resourceName}' (construct: {linkedAnnotation.Construct.GetType().Name}) " +
                $"is not supported as a CloudFront backend behavior origin. The publish target must expose " +
                $"'services__{resourceName}__https__0' or 'services__{resourceName}__http__0' via GetReferenceConnectionInfo.");

        var protocolPolicy = httpsUrl != null ? OriginProtocolPolicy.HTTPS_ONLY : OriginProtocolPolicy.HTTP_ONLY;

        // The URL is a CDK token string such as "https://HOST/" or "http://HOST:PORT/".
        // Use CloudFormation intrinsic functions to extract just the hostname at deploy time.
        // Step 1: strip scheme   → "HOST/" or "HOST:PORT/"
        var withoutScheme = Fn.Select(1, Fn.Split("//", originUrl));
        // Step 2: strip port     → "HOST/" (handles both with-port and without-port)
        // Note: the port value is intentionally discarded — see remarks on this method.
        var withoutPort = Fn.Select(0, Fn.Split(":", withoutScheme));
        // Step 3: strip trailing slash
        var hostname = Fn.Select(0, Fn.Split("/", withoutPort));
        return (hostname, protocolPolicy);
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
file class StaticSiteConnectionPoints : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables { get; set; } = new Dictionary<string, string>();
}
