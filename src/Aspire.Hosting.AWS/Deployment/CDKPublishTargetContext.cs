// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public delegate void PublishCallback<T>(CDKPublishTargetContext context, T props);

/// <summary>
/// The context passed to the CDK props and construct callbacks. It provides access the CDK Stack and the CDKDefaultsProvider.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKPublishTargetContext
{
    private readonly Stack _stack;
    
    internal CDKPublishTargetContext(Stack stack, CDKDefaultsProvider defaultsProvider)
    {
        _stack = stack;
        DefaultsProvider = defaultsProvider;
    }

    /// <summary>
    /// The CDK Stack used as the parent for all the constructs created during publishing.
    /// </summary>
    /// <typeparam name="T">The type used when calling <see cref="AWSCDKEnvironmentExtensions.AddAWSCDKEnvironment"/> with a CDK stack type. If no type was specified then <see cref="Amazon.CDK.Stack"/> should be used.</typeparam>
    /// <returns>The deployment stack</returns>
    /// <exception cref="InvalidCastException">If type T is not the same type used with the <see cref="AWSCDKEnvironmentExtensions.AddAWSCDKEnvironment"/> call</exception>
    public T GetDeploymentStack<T>() where T : Stack
    {
        var typeStack = _stack as T;
        return typeStack ?? throw new InvalidCastException($"The stack {_stack} is not of type {typeof(T)}");
    }
    
    /// <summary>
    /// Gets the <see cref="CDKDefaultsProvider"/> which can be used for getting the default contructs like the default VPC.
    /// </summary>
    public CDKDefaultsProvider DefaultsProvider { get; }
}