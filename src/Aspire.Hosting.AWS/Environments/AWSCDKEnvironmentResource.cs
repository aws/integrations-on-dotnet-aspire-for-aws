// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using AppProps = Amazon.CDK.AppProps;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AWSCDKEnvironmentResource : Resource
{
    /// <summary>
    /// Configuration for creating service clients from the AWS .NET SDK.
    /// </summary>
    public IAWSSDKConfig? AWSSDKConfig { get; set; }

    public DeploymentConstructProvider DeploymentConstructProvider { get; }

    public DefaultProvider DefaultValuesProvider { get; }

    protected AWSCDKEnvironmentResource(string name, DefaultProvider defaultProvider)
    : base(name)
    {
        DefaultValuesProvider = defaultProvider;

        CDKApp = new App(new AppProps
        {
            Outdir = DetermineOutputDirectory()
        });

        DeploymentConstructProvider = new DeploymentConstructProvider(this);
        Annotations.Add(new PipelineStepAnnotation(ConfigurePublishPipelineStep));
        Annotations.Add(new PipelineStepAnnotation(ConfigureDeployPipelineStep));
    }

    internal App CDKApp { get; private init; }

    internal abstract Stack CDKStack { get; }

    private PipelineStep ConfigurePublishPipelineStep(PipelineStepFactoryContext factoryContext)
    {
        var model = factoryContext.PipelineContext.Model;

        var publishStep = new PipelineStep
        {
            Name = $"publish-{Name}",
            Action = async (context) =>
            {
                var cdkCtx = context.Services.GetRequiredService<CDKPublishingStep>();
                await cdkCtx.GenerateCDKOutputAsync(context, model, this);
            },
            RequiredBySteps = [WellKnownPipelineSteps.Publish],
            DependsOnSteps = [WellKnownPipelineSteps.PublishPrereq]
        };
        publishStep.DependsOn(WellKnownPipelineSteps.Build);

        return publishStep;
    }

    private PipelineStep ConfigureDeployPipelineStep(PipelineStepFactoryContext factoryContext)
    {
        var model = factoryContext.PipelineContext.Model;

        var deployStep = new PipelineStep
        {
            Name = $"deploy-{Name}",
            Action = async (context) =>
            {
                var cdkCtx = context.Services.GetRequiredService<CDKDeployStep>();
                await cdkCtx.ExecuteCDKDeployAsync(context, model, this);
            },
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            DependsOnSteps = [WellKnownPipelineSteps.DeployPrereq]
        };
        deployStep.DependsOn(WellKnownPipelineSteps.Publish);

        return deployStep;
    }

    private string DetermineOutputDirectory()
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
    public AWSCDKEnvironmentResource(string name, DefaultProvider defaultProvider, Func<App, T> stackFactory)
        : base(name, defaultProvider)
    {
        EnvironmentStack = stackFactory(CDKApp);
        var stacks = CDKApp.Node.Children
            .OfType<Stack>()
            .ToList();
    }


    public T EnvironmentStack {get; private set;}

    internal override Stack CDKStack => this.EnvironmentStack;
}
