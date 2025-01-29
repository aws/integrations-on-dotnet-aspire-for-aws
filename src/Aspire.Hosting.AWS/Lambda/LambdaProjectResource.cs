// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Aspire resource representing a Lambda function.
/// </summary>
/// <param name="name"></param>
public class LambdaProjectResource(string name) : ProjectResource(name)
{
}
