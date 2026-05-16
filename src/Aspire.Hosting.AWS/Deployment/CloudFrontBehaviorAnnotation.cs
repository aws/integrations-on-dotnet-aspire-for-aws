// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// Annotation added by <c>WithCloudFrontBackendBehavior</c> to record a CloudFront path-pattern
/// routing rule for a backend Aspire resource. The relationship with the backend is established
/// via the standard <see cref="ResourceRelationshipAnnotation"/> (i.e. <c>WithReference</c>) so
/// that Aspire service-discovery env-vars are wired up automatically during publish.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal sealed class CloudFrontBehaviorAnnotation(string pathPattern, IResource backendResource) : IResourceAnnotation
{
    public string PathPattern { get; } = pathPattern;
    public IResource BackendResource { get; } = backendResource;
}
