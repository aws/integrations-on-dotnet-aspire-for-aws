// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Annotation for metadata of a Lambda function resource.
/// </summary>
/// <param name="handler"></param>
internal class LambdaFunctionAnnotation(string handler) : IResourceAnnotation
{
    public string Handler { get; } = handler;
}
