// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments.CDKResourceContexts;
using Constructs;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
/// <summary>
/// This annotation is used for being able to find the CDK construct for the Aspire resource.
/// </summary>
internal class LinkedConstructAnnotations : IResourceAnnotation
{
    /// <summary>
    /// The CDK construct that will be deployed for the Aspire Resource.
    /// </summary>
    public required Construct LinkedConstruct { get; init; }

    public required IAWSPublishTarget PublishTarget { get; init; }
}
