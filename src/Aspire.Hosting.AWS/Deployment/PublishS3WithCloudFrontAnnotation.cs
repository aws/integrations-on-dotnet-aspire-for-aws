// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishS3WithCloudFrontAnnotation : IAWSPublishTargetAnnotation
{
    public PublishS3WithCloudFrontConfig Config { get; } = new();

    /// <summary>
    /// Working directory of the original JavaScript resource, captured at registration time
    /// before Aspire may transform the resource into a different type.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Configuration for publishing a JavaScript application as an S3-backed static website
/// fronted by a CloudFront distribution for HTTPS and global CDN delivery.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishS3WithCloudFrontConfig
{
    /// <summary>
    /// The relative path from the JavaScript application's working directory to the built static
    /// output. Defaults to <c>dist</c>. Angular apps typically use <c>dist/browser</c>.
    /// </summary>
    public string OutputPath { get; set; } = "dist";

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
    /// is created.
    /// </summary>
    public PublishCallback<DistributionProps>? PropsDistributionCallback { get; set; }

    /// <summary>
    /// Callback to modify the created <see cref="Distribution"/> construct.
    /// </summary>
    public PublishCallback<Distribution>? ConstructDistributionCallback { get; set; }
}
