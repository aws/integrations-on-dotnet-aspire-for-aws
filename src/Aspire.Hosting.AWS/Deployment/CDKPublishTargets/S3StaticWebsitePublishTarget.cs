// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.ECS.Patterns;
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
    public override string PublishTargetName => "S3 Static Website";

    public override Type PublishTargetAnnotation => typeof(PublishS3StaticWebsiteAnnotation);

    public override async Task GenerateConstructAsync(
        AWSCDKEnvironmentResource environment,
        IResource resource,
        IAWSPublishTargetAnnotation annotation,
        CancellationToken cancellationToken)
    {
        var publishAnnotation = annotation as PublishS3StaticWebsiteAnnotation
            ?? throw new InvalidOperationException($"Annotation for resource '{resource.Name}' is not a valid {nameof(PublishS3StaticWebsiteAnnotation)}.");

        var config = publishAnnotation.Config;

        var workingDirectory = publishAnnotation.WorkingDirectory
            ?? throw new InvalidOperationException(
                $"Resource '{resource.Name}' is missing a working directory. " +
                $"Ensure PublishAsS3StaticWebsite() is called on a JavaScript resource.");

        // Collect environment variables from Aspire references so they are available during npm build
        var connectionPoints = new StaticSiteConnectionPoints();
        ProcessRelationShips(connectionPoints, resource, environment);
        var buildEnvVars = connectionPoints.EnvironmentVariables ?? new Dictionary<string, string>();

        await siteBuilder.BuildAsync(resource, workingDirectory, buildEnvVars, cancellationToken);

        var buildOutputPath = Path.GetFullPath(Path.Combine(workingDirectory, config.OutputPath));

        var context = CreatePublishTargetContext(environment);

        // --- S3 Bucket ---
        var bucketProps = config.WithCloudFront
            ? new BucketProps
            {
                // Private bucket — CloudFront accesses via OAC
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                EnforceSSL = true,
            }
            : new BucketProps
            {
                // Public S3 website hosting
                WebsiteIndexDocument = "index.html",
                WebsiteErrorDocument = "index.html",
                PublicReadAccess = true,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ACLS_ONLY,
            };

        config.PropsBucketCallback?.Invoke(context, bucketProps);

        var bucket = new Bucket(environment.CDKStack, $"Project-{resource.Name}-Bucket", bucketProps);
        config.ConstructBucketCallback?.Invoke(context, bucket);

        // --- CloudFront distribution (optional) ---
        Distribution? distribution = null;
        if (config.WithCloudFront)
        {
            var distributionProps = new DistributionProps
            {
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = S3BucketOrigin.WithOriginAccessControl(bucket),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                },
                DefaultRootObject = "index.html",
                // SPA routing: map 403 and 404 back to index.html so client-side routes resolve
                ErrorResponses =
                [
                    new ErrorResponse
                    {
                        HttpStatus = 403,
                        ResponseHttpStatus = 200,
                        ResponsePagePath = "/index.html",
                    },
                    new ErrorResponse
                    {
                        HttpStatus = 404,
                        ResponseHttpStatus = 200,
                        ResponsePagePath = "/index.html",
                    },
                ],
            };

            if (config.BackendBehaviors.Count > 0)
            {
                var additionalBehaviors = new Dictionary<string, IBehaviorOptions>();
                foreach (var backendBehavior in config.BackendBehaviors)
                {
                    if (!backendBehavior.BackendResource.TryGetLastAnnotation<AWSLinkedObjectsAnnotation>(out var linkedAnnotation))
                        throw new InvalidOperationException(
                            $"Backend resource '{backendBehavior.BackendResource.Name}' has not been published yet. " +
                            $"Ensure it is defined before the static website resource in your AppHost.");

                    string albDns;
                    if (linkedAnnotation.Construct is ApplicationLoadBalancedFargateService albService)
                        albDns = Token.AsString(albService.LoadBalancer.LoadBalancerDnsName);
                    else
                        throw new InvalidOperationException(
                            $"Backend resource '{backendBehavior.BackendResource.Name}' (construct: {linkedAnnotation.Construct.GetType().Name}) " +
                            $"is not supported as a CloudFront backend behavior origin. Only ECS Fargate with ALB is currently supported.");

                    var behavior = new BehaviorOptions
                    {
                        Origin = new HttpOrigin(albDns, new HttpOriginProps
                        {
                            ProtocolPolicy = OriginProtocolPolicy.HTTP_ONLY,
                        }),
                        AllowedMethods = AllowedMethods.ALLOW_ALL,
                        CachePolicy = CachePolicy.CACHING_DISABLED,
                        OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    };

                    additionalBehaviors[backendBehavior.PathPattern] = behavior;

                    // CloudFront's "/foo/*" pattern matches "/foo/bar" and "/foo/" but NOT "/foo".
                    // Register the bare path as a separate exact-match behavior so requests without
                    // a trailing slash also reach the backend instead of falling through to S3.
                    if (backendBehavior.PathPattern.EndsWith("/*"))
                    {
                        var basePath = backendBehavior.PathPattern[..^2]; // strip trailing "/*"
                        if (!additionalBehaviors.ContainsKey(basePath))
                            additionalBehaviors[basePath] = behavior;
                    }
                }
                distributionProps.AdditionalBehaviors = additionalBehaviors;
            }

            config.PropsDistributionCallback?.Invoke(context, distributionProps);

            distribution = new Distribution(environment.CDKStack, $"Project-{resource.Name}-Distribution", distributionProps);
            config.ConstructDistributionCallback?.Invoke(context, distribution);

            new CfnOutput(environment.CDKStack, $"{resource.Name}-CloudFrontUrl", new CfnOutputProps
            {
                Value = $"https://{distribution.DomainName}",
            });
        }
        else
        {
            new CfnOutput(environment.CDKStack, $"{resource.Name}-S3WebsiteUrl", new CfnOutputProps
            {
                Value = bucket.BucketWebsiteUrl,
            });
        }

        // --- Bucket deployment ---
        var deploymentProps = new BucketDeploymentProps
        {
            Sources = [Source.Asset(buildOutputPath)],
            DestinationBucket = bucket,
        };

        if (distribution != null)
        {
            // Invalidate CloudFront cache on every deployment
            deploymentProps.Distribution = distribution;
            deploymentProps.DistributionPaths = ["/*"];
        }

        config.PropsBucketDeploymentCallback?.Invoke(context, deploymentProps);
        new BucketDeployment(environment.CDKStack, $"Project-{resource.Name}-Deployment", deploymentProps);

        ApplyAWSLinkedObjectsAnnotation(environment, resource, distribution ?? (Constructs.Construct)bucket, this);
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
        => new();

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is JavaScriptAppResource jsResource)
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishS3StaticWebsiteAnnotation { WorkingDirectory = jsResource.WorkingDirectory },
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK,
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
file class StaticSiteConnectionPoints : AbstractCDKConstructConnectionPoints
{
    public override IDictionary<string, string>? EnvironmentVariables { get; set; } = new Dictionary<string, string>();
}
