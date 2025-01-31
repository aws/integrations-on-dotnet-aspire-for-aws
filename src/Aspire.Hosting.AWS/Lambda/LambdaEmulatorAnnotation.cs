// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Annotation for the metadata of a Lambda runtime emulator resource
/// </summary>
/// <param name="endpoint"></param>
internal class LambdaEmulatorAnnotation(EndpointReference endpoint) : IResourceAnnotation
{
    /// <summary>
    /// The HTTP endpoint for the Lambda runtime emulator.
    /// </summary>
    public EndpointReference Endpoint { get; init; } = endpoint;
}
