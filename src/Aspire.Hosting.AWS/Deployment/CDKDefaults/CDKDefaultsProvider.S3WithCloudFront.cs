// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the default root object served by the CloudFront distribution.
    /// </summary>
    /// <remarks>Default is <c>index.html</c>.</remarks>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public virtual string S3WithCloudFrontDefaultRootObject => "index.html";

    /// <summary>
    /// Gets the HTTP status codes that CloudFront rewrites to <see cref="S3WithCloudFrontDefaultRootObject"/>
    /// with a 200 response to support SPA client-side routing.
    /// </summary>
    /// <remarks>Defaults to 403 and 404.</remarks>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public virtual int[] S3WithCloudFrontSpaErrorHttpStatuses => [403, 404];

    /// <summary>
    /// Applies secure defaults to the S3 bucket that backs the CloudFront distribution.
    /// Sets <see cref="BucketProps.BlockPublicAccess"/> and <see cref="BucketProps.EnforceSSL"/>
    /// if they have not already been configured by the user.
    /// </summary>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    protected internal virtual void ApplyS3WithCloudFrontBucketDefaults(BucketProps props)
    {
        if (props.BlockPublicAccess == null)
            props.BlockPublicAccess = BlockPublicAccess.BLOCK_ALL;
        if (!props.EnforceSSL.HasValue)
            props.EnforceSSL = true;
    }

    /// <summary>
    /// Applies defaults to the CloudFront <see cref="DistributionProps"/>.
    /// Fills in <see cref="DistributionProps.DefaultRootObject"/>, SPA error responses, and
    /// the default behaviour's viewer-protocol policy and cache policy if they are not already set.
    /// </summary>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    protected internal virtual void ApplyS3WithCloudFrontDistributionDefaults(DistributionProps props)
    {
        if (props.DefaultRootObject == null)
            props.DefaultRootObject = S3WithCloudFrontDefaultRootObject;

        if (props.ErrorResponses == null)
        {
            props.ErrorResponses = S3WithCloudFrontSpaErrorHttpStatuses
                .Select(status => (IErrorResponse)new ErrorResponse
                {
                    HttpStatus = status,
                    ResponseHttpStatus = 200,
                    ResponsePagePath = $"/{S3WithCloudFrontDefaultRootObject}",
                })
                .ToArray();
        }

        if (props.DefaultBehavior is BehaviorOptions behavior)
        {
            if (behavior.ViewerProtocolPolicy == null)
                behavior.ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS;
            if (behavior.CachePolicy == null)
                behavior.CachePolicy = CachePolicy.CACHING_OPTIMIZED;
        }
    }

    /// <summary>
    /// Applies defaults to a <see cref="BehaviorOptions"/> that routes requests to a backend
    /// service origin. Sets allowed methods, cache policy, origin request policy, and viewer
    /// protocol policy if they have not already been configured by the user.
    /// </summary>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    protected internal virtual void ApplyS3WithCloudFrontBackendBehaviorDefaults(BehaviorOptions behavior)
    {
        if (behavior.AllowedMethods == null)
            behavior.AllowedMethods = AllowedMethods.ALLOW_ALL;
        if (behavior.CachePolicy == null)
            behavior.CachePolicy = CachePolicy.CACHING_DISABLED;
        if (behavior.OriginRequestPolicy == null)
            behavior.OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER;
        if (behavior.ViewerProtocolPolicy == null)
            behavior.ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS;
    }
}
