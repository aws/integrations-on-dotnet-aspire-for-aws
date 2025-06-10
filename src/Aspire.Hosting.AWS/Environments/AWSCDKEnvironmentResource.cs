// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using App = Amazon.CDK.App;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001 

public abstract class AWSCDKEnvironmentResource : Resource
{
    protected AWSCDKEnvironmentResource(string name)
    : base(name)
    {
        Annotations.Add(new PublishingCallbackAnnotation(PublishAsync));
    }

    internal App CDKApp { get; } = new App();

    internal abstract Stack CDKStack { get; }

    private Task PublishAsync(PublishingContext context)
    {
        var cdkCtx = new CDKPublishingContext(
            context.OutputPath,
            context.Services.GetRequiredService<ILambdaDeploymentPackager>(),
            context.Logger);

        return cdkCtx.WriteModelAsync(context.Model, this);
    }
}

public class AWSCDKEnvironmentResource<T> : AWSCDKEnvironmentResource
    where T : Stack 
{
    public AWSCDKEnvironmentResource(string name, Func<App, T> stackFactory)
        : base(name)
    {
        EnvironmentStack = stackFactory(CDKApp);
        var stacks = CDKApp.Node.Children
            .OfType<Stack>()
            .ToList();
    }


    public T EnvironmentStack {get; private set;}

    internal override Stack CDKStack => this.EnvironmentStack;
}
