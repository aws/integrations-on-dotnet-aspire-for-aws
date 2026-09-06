// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.SQS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.Lambda;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

internal sealed class LambdaSQSEventSourceAnnotation(
    IStackResource stack,
    IQueue queue,
    SQSEventSourceOptions? options) : IResourceAnnotation
{
    public IStackResource Stack { get; } = stack;

    public IQueue Queue { get; } = queue;

    public SQSEventSourceOptions? Options { get; } = options;
}
