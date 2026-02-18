// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Constructs;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

/// <summary>
/// Annotation for holding a mapping of the Aspire resource, CDK construct and the publishing target used to create CDK construct from the resource.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AWSLinkedObjectsAnnotation : IResourceAnnotation
{
    /// <summary>
    /// The owning environment for the publish flow.
    /// </summary>
    public required AWSCDKEnvironmentResource EnvironmentResource { get; init; }

    /// <summary>
    /// The Aspire resource being published.
    /// </summary>
    public required IResource Resource { get; init; }
    
    /// <summary>
    /// The CDK construct that will be deployed for the Aspire Resource.
    /// </summary>
    public required Construct Construct { get; init; }

    /// <summary>
    /// The publish target used to create the CDK construct from the Aspire resource. It will be 
    /// used after creation to help setup reference connections between resources.
    /// </summary>
    public required IAWSPublishTarget PublishTarget { get; init; }
}
