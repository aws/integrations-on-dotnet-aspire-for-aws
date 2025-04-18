// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;
using Amazon.Runtime;

namespace Aspire.Hosting.AWS;

/// <summary>
/// Configuration used to construct service client from the AWS SDK for .NET.
/// </summary>
public interface IAWSSDKConfig
{
    /// <summary>
    /// The AWS credential profile to use for resolving credentials to make AWS service API calls.
    /// </summary>
    string? Profile { get; set; }

    /// <summary>
    /// The AWS region to deploy the CloudFormation Stack.
    /// </summary>
    RegionEndpoint? Region { get; set; }

    /// <summary>
    /// Set if the sdk will validate the profile.
    /// </summary>
    public bool SDKValidationEnabled { get; set; }

    internal T CreateServiceConfig<T>() where T : ClientConfig, new()
    {
        var config = new T();

        if (!string.IsNullOrEmpty(Profile))
        {
            config.Profile = new Profile(Profile);
        }

        if (Region != null)
        {
            config.RegionEndpoint = Region;
        }

        return config;
    }
}
