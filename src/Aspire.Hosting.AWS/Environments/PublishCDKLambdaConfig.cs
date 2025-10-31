// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishCDKLambdaConfig
{
    public Action<FunctionProps>? PropsFunctionCallback { get; set; }

    public Action<Function>? ConstructFunctionCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKLambdaAnnotation : IResourceAnnotation
{
    public PublishCDKLambdaConfig Config { get; init; } = new PublishCDKLambdaConfig();
}
