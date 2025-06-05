// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Environments;

internal class PublishingCDKConfigureCallbackAnnotation : IResourceAnnotation
{
    public Action<FunctionProps>? LambdaFunctionPropsCallback { get; set; }

    public Action<Function>? LambdaFunctionConstructCallback { get; set; }
}
