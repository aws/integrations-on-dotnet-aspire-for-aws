// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using App = Amazon.CDK.App;
using AppProps = Amazon.CDK.AppProps;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001 

public abstract class AWSCDKEnvironmentResource : Resource
{
    protected AWSCDKEnvironmentResource(string name)
    : base(name)
    {
        CDKApp = new App(new AppProps
        {
            Outdir = DetermineOuptutDirectory()
        });

        Annotations.Add(new PublishingCallbackAnnotation(PublishAsync));
    }

    internal App CDKApp { get; private init; }

    internal abstract Stack CDKStack { get; }

    private Task PublishAsync(PublishingContext context)
    {
        var cdkCtx = new CDKPublishingContext(
            context.Services.GetRequiredService<IPublishingActivityProgressReporter>(),
            context.Services.GetRequiredService<ILambdaDeploymentPackager>(),
            context.Services.GetRequiredService<ITarballContainerImageBuilder>(),
            context.Logger);

        return cdkCtx.WriteModelAsync(context.Model, this);
    }

    private string DetermineOuptutDirectory()
    {
        string? outputPath = null;
        var args = Environment.GetCommandLineArgs();
        if (args != null)
        {
            for(var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--output-path", StringComparison.CurrentCultureIgnoreCase) || string.Equals(args[i], "-o", StringComparison.CurrentCultureIgnoreCase))
                {
                    outputPath = args[i + 1];
                }
            }
        }

        if (outputPath == null)
        {
            outputPath = Environment.CurrentDirectory;
        }

        if (!string.Equals(new DirectoryInfo(outputPath).Name, "cdk.out"))
        {
            outputPath = Path.Combine(outputPath, "cdk.out");
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        return outputPath;
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
