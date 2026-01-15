using System.Diagnostics.CodeAnalysis;
using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public delegate void PublishCallback<T>(CDKPublishTargetContext context, T props);

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKPublishTargetContext
{
    private readonly Stack _stack;
    
    internal CDKPublishTargetContext(Stack stack, CDKDefaultsProvider defaultsProvider)
    {
        _stack = stack;
        DefaultsProvider = defaultsProvider;
    }

    public T GetDeploymentStack<T>() where T : Stack
    {
        var typeStack = _stack as T;
        return typeStack ?? throw new InvalidCastException($"The stack {_stack} is not of type {typeof(T)}");
    }
    
    public CDKDefaultsProvider DefaultsProvider { get; }
}