// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Constructs;

namespace Aspire.Hosting.AWS.Environments;

/// <summary>
/// This annotation is used for being able to find the CDK construct for the Aspire resource.
/// </summary>
internal class LinkedConstructAnnotations : IResourceAnnotation
{
    /// <summary>
    /// The CDK construct that will be deployed for the Aspire Resource.
    /// </summary>
    public required Construct LinkedConstruct { get; init; }
}
