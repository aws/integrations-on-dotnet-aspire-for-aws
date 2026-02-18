// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishLambdaFunctionAnnotation : IAWSPublishTargetAnnotation
{
    public PublishLambdaFunctionConfig Config { get; set; } = new();
}

/// <summary>
/// The config used for publishing a Lambda Function
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishLambdaFunctionConfig
{
    /// <summary>
    /// Callback to modify the properties used to construct the Function
    /// </summary>
    public PublishCallback<FunctionProps>? PropsFunctionCallback { get; set; }

    /// <summary>
    /// Callback to modify the constructed Function
    /// </summary>
    public PublishCallback<Function>? ConstructFunctionCallback { get; set; }
}
