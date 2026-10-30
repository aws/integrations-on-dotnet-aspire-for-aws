// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.AWS.Deployment;
using Constructs;

namespace Aspire.Hosting.AWS.CDK;

#pragma warning disable ASPIREAWSPUBLISHERS001

internal interface ICDKConstructOutputReference
{
    string? GetValue(AWSCDKEnvironmentResource environment);
}

internal sealed class ConstructOutputReference<T>(
    IResourceWithConstruct<T> construct,
    IStackResource stack,
    ConstructOutputDelegate<T> output,
    StackOutputReference runtimeReference)
    : IValueProvider, IManifestExpressionProvider, IValueWithReferences, ICDKConstructOutputReference
    where T : IConstruct
{
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        runtimeReference.GetValueAsync(cancellationToken);

    public string ValueExpression => runtimeReference.ValueExpression;

    IEnumerable<object> IValueWithReferences.References => [runtimeReference.Resource];

    public string? GetValue(AWSCDKEnvironmentResource environment) =>
        ReferenceEquals(stack.Stack, environment.CDKStack)
            ? output(construct.Construct)
            : null;
}
