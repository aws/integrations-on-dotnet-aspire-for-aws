// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Constructs;

namespace Aspire.Hosting.AWS.Environments;

internal class LinkedConstructAnnotations : IResourceAnnotation
{
    public required Construct LinkedConstruct { get; init; }
}
