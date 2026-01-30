// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// The interface for identifying AWS publish target annotations.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public interface IAWSPublishTargetAnnotation : IResourceAnnotation
{
}
