// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;

namespace Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREAWSPUBLISHERS001

internal sealed class AWSCDKEnvironmentStackResource<T>(
    string name,
    T stack,
    AWSCDKEnvironmentResource<T> environment)
    : StackResource(name, stack), IStackResource<T>, IResourceWithParent<AWSCDKEnvironmentResource<T>>
    where T : Stack
{
    public new T Stack { get; } = stack;

    public new T Construct => Stack;

    public AWSCDKEnvironmentResource<T> Parent { get; } = environment;
}
