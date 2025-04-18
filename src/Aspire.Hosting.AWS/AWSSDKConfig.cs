// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;

namespace Aspire.Hosting.AWS;

internal sealed class AWSSDKConfig : IAWSSDKConfig
{
    /// <inheritdoc/>
    public string? Profile { get; set; }

    /// <inheritdoc/>
    public RegionEndpoint? Region { get; set; }

    /// <inheritdoc/>
    public bool SDKValidationEnabled { get; set; } = true;
}
