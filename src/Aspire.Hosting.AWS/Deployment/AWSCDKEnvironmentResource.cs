// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.SecurityToken;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Provisioning;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.AWS.Utils;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using App = Amazon.CDK.App;
using AppProps = Amazon.CDK.AppProps;
using Environment = System.Environment;
using Resource = Aspire.Hosting.ApplicationModel.Resource;
using Stack = Amazon.CDK.Stack;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREPUBLISHERS001

/// <summary>
/// The environment resource used to transform the resources defined in the Aspire AppHost into AWS CDK constructs and deploy them to AWS.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AWSCDKEnvironmentResource : Resource, IComputeEnvironmentResource
{
    internal const string CDK_CONTEXT_JSON_ENV_VARIABLE = "CDK_CONTEXT_JSON";
    internal const string CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE = "AWS_ASPIRE_CONTEXT_GENERATION_PATH";

    /// <summary>
    /// Gets the configuration for the <see cref="AWSCDKEnvironmentResource"/>.
    /// </summary>
    public AWSCDKEnvironmentResourceConfig Config { get; }

    protected bool IsPublishMode { get; }

    /// <summary>
    /// Gets the <see cref="CDKDefaultsProvider"/> for the <see cref="AWSCDKEnvironmentResource"/>. The default provider is used to provide 
    /// default values like memory sizes, cpu limits, expected ports, etc. for various AWS CDK constructs created as part of the Aspire deployment.
    /// It also provides default CDK constructs like VPCs, ECS Clusters, Security Groups, etc. that can be shared across multiple resources.
    /// </summary>
    public CDKDefaultsProvider DefaultsProvider { get; }

    protected AWSCDKEnvironmentResource(string name, bool isPublishMode, CDKDefaultsProviderFactory cdkDefaultsProviderFactory, AWSCDKEnvironmentResourceConfig? environmentResourceConfig)
    : base(name)
    {
        IsPublishMode = isPublishMode;
        DefaultsProvider = cdkDefaultsProviderFactory.Create(this);
        Config = environmentResourceConfig ?? new AWSCDKEnvironmentResourceConfig();

        Annotations.Add(new PipelineStepAnnotation(ConfigurePublishPipelineStep));
        Annotations.Add(new PipelineStepAnnotation(ConfigureDeployPipelineStep));
    }

    private App? _cdkApp;
    internal App CDKApp 
    { 
        get
        {
            if (_cdkApp == null)
            {
                throw new InvalidOperationException("CDK App has not been initialized. Ensure InitializeCDKApp has been called before accessing the CDKApp property.");
            }

            return _cdkApp;
        }
    }

    internal void InitializeCDKApp(ILogger? logger, string outputDir)
    {
        SystemCapabilityEvaluator.CheckNodeInstallationAsync().GetAwaiter().GetResult();

        var appProps = new AppProps();
        if (IsPublishMode)
        {
            appProps.Outdir = outputDir;

            var cdkContext = GetCDKContext(logger);
            if (cdkContext != null)
            {
                appProps.Context = cdkContext;
            }
        }

        _cdkApp = new App(appProps);
    }

    protected virtual IDictionary<string, object>? GetCDKContext(ILogger? logger) => null;

    /// <summary>
    /// The CDK context comes from the CDK CLI and is what is used to resolve lookups like Vpc.FromLookup, etc.
    /// 
    /// See the override GetCDKContext method for more details about the fork mode and CDK context.
    /// </summary>
    internal string? CDKContextGenerationLog
    {
        get; set;
    }

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

    protected IEnvironment GetCDKEnvironment()
    {
        var environment = new Amazon.CDK.Environment();

        if (Config.AWSSDKConfig?.Region != null)
        {
            environment.Region = Config.AWSSDKConfig.Region.SystemName;
        }
        else
        {
            try
            {
                environment.Region = FallbackRegionFactory.GetRegionEndpoint()?.SystemName;
            }
            catch
            {
                // ignored
            }
        }

        AWSCredentials? awsCredentials = null;
        if (Config.AWSSDKConfig?.Profile != null)
        {
            var config = Config.AWSSDKConfig.CreateServiceConfig<AmazonCloudFormationConfig>();
            awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
        }
        else
        {
            try
            {
                awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();
            }
            catch
            {
                // ignored
            }
        }

        if (environment.Region != null && awsCredentials != null)
        {
            var stsConfig = new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(environment.Region),
                DefaultAWSCredentials = awsCredentials
            };
            using var stsClient = new AmazonSecurityTokenServiceClient(stsConfig);

            var callerIdentityResponse = stsClient.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest()).GetAwaiter().GetResult();
            environment.Account = callerIdentityResponse.Account;
        }

        return environment;
    }

    internal AmazonCloudFormationClient GetCloudFormationClient()
    {
        try
        {
            AmazonCloudFormationClient client;
            if (Config.AWSSDKConfig != null)
            {
                var config = Config.AWSSDKConfig.CreateServiceConfig<AmazonCloudFormationConfig>();

                var awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
                client = new AmazonCloudFormationClient(awsCredentials, config);
            }
            else
            {
                client = new AmazonCloudFormationClient();
            }

            client.BeforeRequestEvent += SdkUtilities.ConfigureUserAgentString;

            return client;
        }
        catch (Exception e)
        {
            throw new AWSProvisioningException("Failed to construct AWS CloudFormation service client to provision AWS resources.", e);
        }
    }
}

/// <summary>
/// The environment resource used to transform the resources defined in the Aspire AppHost into AWS CDK constructs and deploy them to AWS.
/// </summary>
/// <typeparam name="T">The CDK Stack type to create for the environment. It can be used to define additional CDK constructs as part of deployment or override the default constructs that would be created by the CDKDefaultsProvider.</typeparam>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AWSCDKEnvironmentResource<T> : AWSCDKEnvironmentResource
    where T : Stack 
{
    readonly Func<App, IStackProps, T> _stackFactory;

    internal AWSCDKEnvironmentResource(string name, bool isPublishMode, CDKDefaultsProviderFactory cdkDefaultsProviderFactory, Func<App, IStackProps, T> stackFactory, AWSCDKEnvironmentResourceConfig? environmentResourceConfig)
        : base(name, isPublishMode, cdkDefaultsProviderFactory, environmentResourceConfig)
    {
        _stackFactory = stackFactory;

        // Running in the fork mode so we need to force load the stack during the constructor
        // so the CDK cli can validate it has all of the required information for the CDK context.
        //
        // See the override GetCDKContext method for more details about the fork mode and CDK context.
        if (Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE) != null)
        {
            InitializeCDKApp(null, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            LoadEnvironmentStack();
        }
    }

    private T? _stack;
    private void LoadEnvironmentStack()
    {
        var props = new StackProps();
        props.Env = GetCDKEnvironment();
        try
        {
            _stack = _stackFactory(CDKApp, props);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Configure \"env\" with an account and region"))
            {
                throw new InvalidOperationException(
                    "CDK Stack is using constructs that require the account and region information during publishing. " +
                    "Ensure either there is a default AWS credentials and region configured for the environment or use " +
                    "the AddAWSSDKConfig extension method to create an SDK config and pass the sdk config in with " +
                    "the AddAWSCDKEnvironment method as part of the AWSCDKEnvironmentResourceConfig.");
            }

            throw;
        }
    }

    internal override Stack CDKStack
    {
        get
        {
            if (_stack == null)
                LoadEnvironmentStack();
            
            return _stack!;
        }
    }

    protected override IDictionary<string, object>? GetCDKContext(ILogger? logger)
    {
        try
        {
            if (!SystemCapabilityEvaluator.IsCDKInstalled())
            {
                logger?.LogWarning("AWS CDK CLI is not installed. The CDK CLI is recommended for publish step and required for deployment step. " + 
                    "Skipping CDK context generation. If the CDK stack references any existing resources like the default VPC, CDK will generate the CloudFormation template with incorrect placeholder values.");
                return null;
            }

            // If there is no SDK config applied to the environment then we can't 
            // configure the CDK environment and thus can't generate the context.
            // It is okay to not have a context but if the user uses any lookup constructs
            // Vpc.FromLookup that will fail during publish time. There is error handling
            // else where to catch that scenario and inform the user.
            var cdkEnvironment = GetCDKEnvironment();
            if (cdkEnvironment.Account == null || cdkEnvironment.Region == null)
                return null;

            var cdkContextJsonTempPath = Path.GetTempFileName();
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE)))
            {
                const string cdkContextFileName = "cdk.context.json";

                using var cfClient = GetCloudFormationClient();
                var environmentVariables = SdkUtilities.CreateDictionaryOfAWSCredentialsAndRegion(cfClient);
                environmentVariables[CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE] = cdkContextJsonTempPath;

                var fullPath = Assembly.GetEntryAssembly()!.Location;
                var appHostAssembly = Config.OverrideAppHostAssemblyName ?? Path.GetFileName(fullPath);
                string workingDirectory = Directory.GetParent(fullPath)!.FullName;
                var outputPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
                Directory.CreateDirectory(outputPath);

                // Clean up any existing cdk.context.json file in the working directory to avoid reusing stale context.
                if (File.Exists(Path.Combine(workingDirectory, cdkContextFileName)))
                {
                    File.Delete(Path.Combine(workingDirectory, cdkContextFileName));
                }

                // Essentially fork the Aspire process running from the CDK cli which will handle generating the CDK context. In the fork the code will go into the following "else" block.
                // The fork else block will write the CDK context to location specified by cdkContextJsonTempPath.
                var processCommandService = new ProcessCommandService();
                var result = processCommandService.RunCDKProcess(null, LogLevel.Warning, $"--app \'dotnet exec {appHostAssembly} --operation publish --step publish\' synth --verbose --output \'{outputPath}\'", workingDirectory, environmentVariables);
                CDKContextGenerationLog = result.Output;

                if (result.ExitCode != 0)
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    // If the CDK CLI generated a CDK context it will put the value data in the CDK_CONTEXT_JSON_ENV_VARIABLE environment variable.
                    // Store the content in the location specified by the parent fork in the "if" block above.
                    var cdkContextJsonContent = Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_ENV_VARIABLE);
                    if (!string.IsNullOrEmpty(cdkContextJsonContent))
                    {
                        var cdkContextJsonOutputPath = Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE);
                        if (string.IsNullOrEmpty(cdkContextJsonOutputPath))
                        {
                            throw new InvalidOperationException($"Environment variable {CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE} is not set. Cannot determine output path for CDK context JSON.");
                        }

                        File.WriteAllText(cdkContextJsonOutputPath, cdkContextJsonContent);
                    }

                    // Create a new CDK app instead of using the CDKApp property to avoid recussive calls to GetCDKContext.
                    var app = new App();
                    var props = new StackProps
                    {
                        Env = cdkEnvironment
                    };
                    var stack = _stackFactory(app, props);

                    // Add a stub VPC to the stack for generating the context to ensure the availability zones are captured in the context.
                    // Of the constructs that the Aspire integration uses that require context generation, VPC is the only one that needs it.
                    // Any other constructs that require CDK context should be defined by the user in the stack created by stack factory passed in to
                    // the environment resource by the user.
                    new Vpc(stack, "__PlaceHolderVpc__", new VpcProps
                    {
                        MaxAzs = 2
                    });
                    
                    app.Synth();

                    // Exit successfully to inform the parent fork that the context generation succeeded.
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(-1);
                }
            }

            var cdkContextJson = File.ReadAllText(cdkContextJsonTempPath);
            using var doc = JsonDocument.Parse(cdkContextJson);
            var context = (IDictionary<string, object>)doc.RootElement.ConvertToDotNetPrimitives();
            return context;
        }
        catch
        {
            return null;
        }
    }
}
