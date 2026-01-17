// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// Container for the optional configuration settings for the <see cref="AWSCDKEnvironmentResource"/>.
/// </summary>
public class AWSCDKEnvironmentResourceConfig
{
    /// <summary>
    /// The AWS SDK configuration to use when publishing an deploying to AWS. If not set
    /// the region and credential information will be inferred from the environment.
    /// </summary>
    public IAWSSDKConfig? AWSSDKConfig { get; init; }
}
