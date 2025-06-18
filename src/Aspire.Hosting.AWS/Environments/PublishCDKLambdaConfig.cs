// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Environments;

public class PublishCDKLambdaConfig
{
    public Action<FunctionProps>? PropsCallback { get; set; }

    public Action<Function>? ConstructCallback { get; set; }
}

internal class PublishCDKLambdaAnnotation : IResourceAnnotation
{
    public required PublishCDKLambdaConfig Config { get; init; }
}
