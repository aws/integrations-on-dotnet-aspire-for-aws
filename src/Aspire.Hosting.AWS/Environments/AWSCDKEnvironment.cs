// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

using Stack = Amazon.CDK.Stack;
using App = Amazon.CDK.App;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001 

public abstract class AWSCDKEnvironment : Resource
{
    protected AWSCDKEnvironment(string name)
    : base(name)
    {
        Annotations.Add(new PublishingCallbackAnnotation(PublishAsync));
    }

    internal App CDKApp { get; } = new App();

    internal abstract Stack CDKStack { get; }

    private Task PublishAsync(PublishingContext context)
    {
        ILambdaDeploymentPackager lambdaDeploymentPackager = context.Services.GetRequiredService<ILambdaDeploymentPackager>();
        var cdkCtx = new CDKPublishingContext(
            context.OutputPath,
            lambdaDeploymentPackager,
            context.Logger);

        return cdkCtx.WriteModelAsync(context.Model, this);
    }
}

public class AWSCDKEnvironment<T> : AWSCDKEnvironment
    where T : Stack 
{
    public AWSCDKEnvironment(string name, Func<App, T> stackFactory)
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
