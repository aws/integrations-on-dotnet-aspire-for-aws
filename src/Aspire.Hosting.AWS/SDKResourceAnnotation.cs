// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS;

/// <summary>
/// Annotation to attach to resources to indicate what AWS SDK config the resource is configured for.
/// </summary>
/// <param name="awsSdkConfig"></param>
internal class SDKResourceAnnotation(IAWSSDKConfig awsSdkConfig) : IResourceAnnotation
{
    /// <summary>
    /// The AWS SDK config for the SDK to use to connect to AWS.
    /// </summary>
    public IAWSSDKConfig SdkConfig { get; set; } = awsSdkConfig;
}
