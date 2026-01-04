// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.SecurityToken;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using AppProps = Amazon.CDK.AppProps;
using Environment = System.Environment;
using Resource = Aspire.Hosting.ApplicationModel.Resource;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AWSCDKEnvironmentResource : Resource
{
    /// <summary>
    /// Configuration for creating service clients from the AWS .NET SDK.
    /// </summary>
    public IAWSSDKConfig? AWSSDKConfig { get; set; }

    public CDKDefaultsProvider DefaultsProvider { get; }

    protected AWSCDKEnvironmentResource(string name, CDKDefaultsProviderFactory cdkDefaultsProviderFactory)
    : base(name)
    {
        DefaultsProvider = cdkDefaultsProviderFactory.Create(this);

        CDKApp = new App(new AppProps
        {
            Outdir = DetermineOutputDirectory()
        });
        
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

    protected Amazon.CDK.IEnvironment GetCDKEnvironment()
    {
        var environment = new Amazon.CDK.Environment();

        if (AWSSDKConfig?.Region != null)
        {
            environment.Region = AWSSDKConfig.Region.SystemName;
        }
        else
        {
            try
            {
                environment.Region = FallbackRegionFactory.GetRegionEndpoint()?.SystemName;
            }
            catch { }
        }

        AWSCredentials? awsCredentials = null;
        if (AWSSDKConfig?.Profile != null)
        {
            var config = AWSSDKConfig.CreateServiceConfig<AmazonCloudFormationConfig>();
            awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
        }
        else
        {
            try
            {
                awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();
            }
            catch { }
        }

        if (environment.Region != null && awsCredentials != null)
        {
            var stsConfig = new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(environment.Region),
                DefaultAWSCredentials = awsCredentials
            };
            var stsClient = new AmazonSecurityTokenServiceClient(stsConfig);

            var callerIdentityResponse = stsClient.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest()).GetAwaiter().GetResult();
            environment.Account = callerIdentityResponse.Account;
        }

        return environment;
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AWSCDKEnvironmentResource<T> : AWSCDKEnvironmentResource
    where T : Stack 
{
    Func<App, IStackProps, T> _stackFactory;
    public AWSCDKEnvironmentResource(string name, CDKDefaultsProviderFactory cdkDefaultsProviderFactory, Func<App, IStackProps, T> stackFactory)
        : base(name, cdkDefaultsProviderFactory)
    {
        _stackFactory = stackFactory;
    }

    T? _environmentStack;
    public T EnvironmentStack 
    {
        get
        {
            if (_environmentStack == null)
            {
                var props = new StackProps();
                props.Env = GetCDKEnvironment();
                try
                {
                    _environmentStack = _stackFactory(CDKApp, props);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Configure \"env\" with an account and region"))
                    {
                        throw new InvalidOperationException(
                            "CDK Stack is using constructs that require the account and region information during publishing. " +
                            "Ensure either there is a default AWS credentials and region configured for the environment or use " +
                            "the AddAWSSDKConfig extension method to create an SDK config and then call WithReference on " +
                            "the AddAWSCDKEnvironment return with the SDK config.");
                    }

                    throw;
                }
            }

            return _environmentStack;
        }
    }

    internal override Stack CDKStack => this.EnvironmentStack;
}
