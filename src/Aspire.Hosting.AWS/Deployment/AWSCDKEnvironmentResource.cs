// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.SecurityToken;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Provisioning;
using Aspire.Hosting.AWS.Utils.Internal;
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

namespace Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AWSCDKEnvironmentResource : Resource
{
    internal const string CDK_CONTEXT_JSON_ENV_VARIABLE = "CDK_CONTEXT_JSON";
    internal const string CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE = "AWS_ASPIRE_CONTEXT_GENERATION_PATH";

    /// <summary>
    /// Configuration for creating service clients from the AWS .NET SDK.
    /// </summary>
    public IAWSSDKConfig? AWSSDKConfig { get; set; }

    public CDKDefaultsProvider DefaultsProvider { get; }

    protected AWSCDKEnvironmentResource(string name, CDKDefaultsProviderFactory cdkDefaultsProviderFactory)
    : base(name)
    {
        DefaultsProvider = cdkDefaultsProviderFactory.Create(this);

        Annotations.Add(new PipelineStepAnnotation(ConfigurePublishPipelineStep));
        Annotations.Add(new PipelineStepAnnotation(ConfigureDeployPipelineStep));
    }

    App? _cdkApp;
    internal App CDKApp 
    { 
        get
        {
            if (_cdkApp == null)
            {
                var appProps = new AppProps();

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE)))
                {
                    appProps.Outdir = DetermineOutputDirectory();
                }

                var cdkContext = GetCDKContext();
                if (cdkContext != null)
                {
                    appProps.Context = cdkContext;
                }


                _cdkApp = new App(appProps);
            }

            return _cdkApp;
        }
    }

    protected virtual IDictionary<string, object>? GetCDKContext() => null;

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

    internal AmazonCloudFormationClient GetCloudFormationClient()
    {
        try
        {
            AmazonCloudFormationClient client;
            if (AWSSDKConfig != null)
            {
                var config = AWSSDKConfig.CreateServiceConfig<AmazonCloudFormationConfig>();

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

    protected override IDictionary<string, object>? GetCDKContext()
    {
        try
        {
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
                using var cfClient = GetCloudFormationClient();
                var environmentVariables = SdkUtilities.CreateDictionaryOfAWSCredentialsAndRegion(cfClient);
                environmentVariables[CDK_CONTEXT_JSON_OUTPUT_ENV_VARIABLE] = cdkContextJsonTempPath;

                var fullPath = Assembly.GetEntryAssembly()!.Location;
                var appHostAssembly = Path.GetFileName(fullPath);
                string? workingDirectory = Directory.GetParent(fullPath)!.FullName;
                var outputPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
                Directory.CreateDirectory(outputPath);

                // Essentially fork the Aspire process running from the CDK cli which will handle generating the CDK context. In the fork the code will go into the following "else" block.
                // The fork else block will write the CDK context to location specified by cdkContextJsonTempPath.
                var processCommandService = new ProcessCommandService();
                var result = processCommandService.RunCDKProcess(null, LogLevel.Warning, $"--app \'dotnet exec {appHostAssembly} --operation publish --step publish\' synth --output \'{outputPath}\'", workingDirectory, environmentVariables);
                CDKContextGenerationLog = result.Output;

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"CDK context generation failed with exit code {result.ExitCode}");
                }
            }
            else
            {
                try
                {
                    // If the CDK CLI generated a CDK context it will put the value data in the CDK_CONTEXT_JSON_ENV_VARIABLE environment variable.
                    // Store the content in the location specified by the parent fork in the "if" block above.
                    var cdkContextJsonContent = System.Environment.GetEnvironmentVariable(CDK_CONTEXT_JSON_ENV_VARIABLE);
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
                    var props = new StackProps();
                    props.Env = cdkEnvironment;
                    _stackFactory(app, props);
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
            var context = (IDictionary<string, object>)ConvertElement(doc.RootElement);
            return context;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Utility method to convert a JsonElement into a dictionary/array/primitive object. If we don't
    /// do this CDK will complain about unmapped types over JSII.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    static object ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertElement(prop.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertElement(item));
                }
                return list.ToArray();

            case JsonValueKind.String:
                return element.GetString()!;

            case JsonValueKind.Number:
                // CDK context supports both int and double
                if (element.TryGetInt64(out var l))
                    return l;
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null!;

            default:
                throw new NotSupportedException($"Unsupported JSON token: {element.ValueKind}");
        }
    }
}
