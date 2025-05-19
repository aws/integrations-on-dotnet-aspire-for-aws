// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;

namespace Aspire.Hosting.AWS.CDK;

/// <summary>
/// Resource representing an AWS CDK stack.
/// </summary>
public interface IStackResource : ICloudFormationTemplateResource, IResourceWithConstruct, IResourceWithWaitSupport
{
    /// <summary>
    /// The AWS CDK stack
    /// </summary>
    Stack Stack { get; }
}
