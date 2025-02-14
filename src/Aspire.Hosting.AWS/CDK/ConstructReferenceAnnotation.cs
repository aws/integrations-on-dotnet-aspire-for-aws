// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.CDK;

/// <summary>
/// Annotations that records a reference of Construct resources to target resources like projects.
/// </summary>
/// <param name="targetResource"></param>
/// <param name="outputName"></param>
[DebuggerDisplay("Type = {GetType().Name,nq}, TargetResource = {TargetResource}, OutputName = {OutputName}")]
internal sealed class ConstructReferenceAnnotation(string targetResource, string outputName) : IResourceAnnotation
{
    /// <summary>
    /// The name of the target resource.
    /// </summary>
    internal string TargetResource { get; } = targetResource;

    /// <summary>
    /// The name of the output.
    /// </summary>
    internal string OutputName { get; } = outputName;
}
