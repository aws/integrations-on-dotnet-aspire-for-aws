// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Constructs;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

/// <summary>
/// This annotation is used for being able to find the CDK construct for the Aspire resource.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AWSLinkedObjectsAnnotation : IResourceAnnotation
{
    public required AWSCDKEnvironmentResource EnvironmentResource { get; init; }
    
    public required IResource Resource { get; init; }
    
    /// <summary>
    /// The CDK construct that will be deployed for the Aspire Resource.
    /// </summary>
    public required Construct Construct { get; init; }

    public required IAWSPublishTarget PublishTarget { get; init; }
}
