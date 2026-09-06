// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

internal sealed class ConstructGrantAnnotation(IStackResource stack, Action<IGrantable> grant) : IResourceAnnotation
{
    public IStackResource Stack { get; } = stack;

    public void Apply(IGrantable grantee) => grant(grantee);
}
