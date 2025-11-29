// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using AppProps = Amazon.CDK.AppProps;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public enum DeploymentComputeService { ECSFargate }

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AWSCDKEnvironmentResource : Resource
{
    public DeploymentComputeService PreferredComputeService { get; private set; }

    public DeploymentConstructProvider DeploymentConstructProvider { get; }

    public DefaultProvider DefaultValuesProvider { get; }

    protected AWSCDKEnvironmentResource(string name, DeploymentComputeService preferredComputeService, DefaultProvider defaultProvider)
    : base(name)
    {
        PreferredComputeService = preferredComputeService;
        DefaultValuesProvider = defaultProvider;

        CDKApp = new App(new AppProps
        {
            Outdir = DetermineOuptutDirectory()
        });

        DeploymentConstructProvider = new DeploymentConstructProvider(this);
        Annotations.Add(new PipelineStepAnnotation(ConfigurePipeline));
    }

    internal App CDKApp { get; private init; }

    internal abstract Stack CDKStack { get; }

    private PipelineStep ConfigurePipeline(PipelineStepFactoryContext factoryContext)
    {
        var model = factoryContext.PipelineContext.Model;

        var step = new PipelineStep
        {
            Name = $"publish-{Name}",
            Action = async (context) =>
            {
                var cdkCtx = context.Services.GetRequiredService<CDKPublishingContext>();
                await cdkCtx.WriteModelAsync(context, model, this);
            },
            RequiredBySteps = [WellKnownPipelineSteps.Publish],
            DependsOnSteps = [WellKnownPipelineSteps.PublishPrereq]
        };
        step.DependsOn(WellKnownPipelineSteps.Build);

        var cdkCtx = factoryContext.PipelineContext.Services.GetRequiredService<CDKPublishingContext>();
        return step;
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

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AWSCDKEnvironmentResource<T> : AWSCDKEnvironmentResource
    where T : Stack 
{
    public AWSCDKEnvironmentResource(string name, DeploymentComputeService preferredComputeService, DefaultProvider defaultProvider, Func<App, T> stackFactory)
        : base(name, preferredComputeService, defaultProvider)
    {
        EnvironmentStack = stackFactory(CDKApp);
        var stacks = CDKApp.Node.Children
            .OfType<Stack>()
            .ToList();
    }


    public T EnvironmentStack {get; private set;}

    internal override Stack CDKStack => this.EnvironmentStack;
}
