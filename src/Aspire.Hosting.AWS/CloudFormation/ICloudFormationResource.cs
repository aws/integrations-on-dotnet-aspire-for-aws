// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.CloudFormation;

/// <summary>
/// Resource representing an AWS CloudFormation stack.
/// </summary>
public interface ICloudFormationResource : IAWSResource, IResourceWithWaitSupport
{
    /// <summary>
    /// The configured Amazon CloudFormation service client used to make service calls. If this property set
    /// then AWSSDKConfig is ignored.
    /// </summary>
    IAmazonCloudFormation? CloudFormationClient { get; set; }

    /// <summary>
    /// The name of the Amazon CloudFormation stack
    /// </summary>
    string StackName { get; }

    /// <summary>
    /// The output parameters of the CloudFormation stack.
    /// </summary>
    List<Output>? Outputs { get; }

    /// <summary>
    /// Custom tags to apply to the CloudFormation stack.
    /// </summary>
    IDictionary<string, string> Tags { get; }
}
