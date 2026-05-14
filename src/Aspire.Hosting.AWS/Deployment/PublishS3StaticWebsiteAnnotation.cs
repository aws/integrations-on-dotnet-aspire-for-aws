// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishS3StaticWebsiteAnnotation : IAWSPublishTargetAnnotation
{
    public PublishS3StaticWebsiteConfig Config { get; } = new();

    /// <summary>
    /// Working directory of the original JavaScript resource, captured at registration time
    /// before Aspire may transform the resource into a different type.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Configuration for publishing a JavaScript application as a static website on S3.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishS3StaticWebsiteConfig
{
    /// <summary>
    /// When <see langword="true"/>, a CloudFront distribution is placed in front of the S3 bucket.
    /// The bucket is kept private and accessed via Origin Access Control (OAC).
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool WithCloudFront { get; set; } = false;

    /// <summary>
    /// The relative path from the JavaScript application's working directory to the built static
    /// output. Defaults to <c>dist</c>. Angular apps typically use <c>dist/browser</c>.
    /// </summary>
    public string OutputPath { get; set; } = "dist";

    /// <summary>
    /// Backend behaviors that add a CloudFront origin for a specific path pattern, routing
    /// matched requests to a backend resource (e.g. an ECS Fargate service with ALB) instead
    /// of serving them from S3. Only used when <see cref="WithCloudFront"/> is <see langword="true"/>.
    /// </summary>
    public List<BackendBehavior> BackendBehaviors { get; } = [];

    /// <summary>
    /// Adds a CloudFront behavior that routes requests matching <paramref name="pathPattern"/>
    /// to the given <paramref name="backendResource"/> instead of serving them from S3.
    /// Requires <see cref="WithCloudFront"/> to be <see langword="true"/>.
    /// </summary>
    /// <param name="pathPattern">
    /// The CloudFront path pattern, e.g. <c>/api/*</c> or <c>/agents/*</c>.
    /// </param>
    /// <param name="backendResource">
    /// The backend Aspire resource. Must be deployed as an ECS Fargate service with ALB and
    /// appear before the static website resource in the AppHost.
    /// </param>
    public PublishS3StaticWebsiteConfig AddBackendBehavior(string pathPattern, IResource backendResource)
    {
        BackendBehaviors.Add(new BackendBehavior(pathPattern, backendResource));
        return this;
    }

    /// <summary>
    /// Adds a CloudFront behavior that routes requests matching <paramref name="pathPattern"/>
    /// to the given <paramref name="backendResourceBuilder"/> instead of serving them from S3.
    /// Requires <see cref="WithCloudFront"/> to be <see langword="true"/>.
    /// </summary>
    public PublishS3StaticWebsiteConfig AddBackendBehavior<T>(string pathPattern, IResourceBuilder<T> backendResourceBuilder)
        where T : IResource
        => AddBackendBehavior(pathPattern, backendResourceBuilder.Resource);

    /// <summary>
    /// Callback to modify the <see cref="BucketProps"/> before the S3 bucket is created.
    /// </summary>
    public PublishCallback<BucketProps>? PropsBucketCallback { get; set; }

    /// <summary>
    /// Callback to modify the created <see cref="Bucket"/> construct.
    /// </summary>
    public PublishCallback<Bucket>? ConstructBucketCallback { get; set; }

    /// <summary>
    /// Callback to modify the <see cref="BucketDeploymentProps"/> before the deployment is created.
    /// </summary>
    public PublishCallback<BucketDeploymentProps>? PropsBucketDeploymentCallback { get; set; }

    /// <summary>
    /// Callback to modify the <see cref="DistributionProps"/> before the CloudFront distribution
    /// is created. Only invoked when <see cref="WithCloudFront"/> is <see langword="true"/>.
    /// </summary>
    public PublishCallback<DistributionProps>? PropsDistributionCallback { get; set; }

    /// <summary>
    /// Callback to modify the created <see cref="Distribution"/> construct.
    /// Only invoked when <see cref="WithCloudFront"/> is <see langword="true"/>.
    /// </summary>
    public PublishCallback<Distribution>? ConstructDistributionCallback { get; set; }
}

/// <summary>
/// A CloudFront path-pattern-to-backend routing rule for use with <see cref="PublishS3StaticWebsiteConfig.AddBackendBehavior"/>.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public record BackendBehavior(string PathPattern, IResource BackendResource);
