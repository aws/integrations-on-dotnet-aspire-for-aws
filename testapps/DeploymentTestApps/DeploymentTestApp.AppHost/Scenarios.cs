using Amazon.CDK;
using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Deployment;

using Environment = System.Environment;

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREAWSAGENTCORE001

namespace DeploymentTestApp.AppHost
{
    public static class Scenarios
    {
        static readonly AWSCDKEnvironmentResourceConfig _defaultEnvironentResourceConfig = new AWSCDKEnvironmentResourceConfig
        {
            OverrideAppHostAssemblyName = "DeploymentTestApp.AppHost.dll"
        };

        public static async Task PublishWebApp2ReferenceOnWebApp1()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            Console.WriteLine("Running in publish mode: " + builder.ExecutionContext.IsPublishMode);
            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWebApp2ReferenceOnWebApp1));

            var webApp1 = builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .PublishAsECSFargateExpressService(new PublishECSFargateExpressServiceConfig
                {
                    PropsFargateTaskDefinitionCallback = (context, props) =>
                    {
                        props.MemoryLimitMiB = 4096;
                    }
                })
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("WebApp2", "../DeploymentTestApps.WebApp2")
                .WithReference(webApp1)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishWebApp2ReferenceOnWebApp1WithAlb()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWebApp2ReferenceOnWebApp1WithAlb));

            var webApp1 = builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .PublishAsECSFargateServiceWithALB(new PublishECSFargateServiceWithALBConfig
                {
                    PropsApplicationLoadBalancedFargateServiceCallback = (context, props) =>
                    {
                        props.MemoryLimitMiB = 4096;
                    }
                })
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("WebApp2", "../DeploymentTestApps.WebApp2")
                .PublishAsECSFargateServiceWithALB()
                .WithReference(webApp1)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishService1ReferenceOnWebApp1()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishService1ReferenceOnWebApp1));

            var webApp1 = builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("Service1", "../DeploymentTestApp.Service1")
                .WithReference(webApp1);

            await ExecuteApp(builder);
        }

        public static async Task PublishWebApp1UsingDefaultVpc()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new DefaultVpcStack(app, nameof(PublishWebApp1UsingDefaultVpc), props), _defaultEnvironentResourceConfig);

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishLambda()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishLambda));

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler");

            await ExecuteApp(builder);
        }

        public static async Task PublishLambdaWithCustomization()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new PublishLambdaWithCustomization(app, nameof(PublishLambdaWithCustomization), props), _defaultEnvironentResourceConfig);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                    .PublishAsLambdaFunction(new PublishLambdaFunctionConfig
                    {
                        PropsFunctionCallback = (cts, props) =>
                        {
                            props.MemorySize = 2048;
                            props.Timeout = Duration.Seconds(120);
                        },
                        ConstructFunctionCallback = (ctx, construct) =>
                        {
                            construct.AddEventSource(new SqsEventSource(ctx.GetDeploymentStack<PublishLambdaWithCustomization>().LambdaQueue, new SqsEventSourceProps
                            {
                                BatchSize = 5,
                                Enabled = true
                            }));
                        }
                    });

            await ExecuteApp(builder);
        }

        public static async Task PublishLambdaWithReferences()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new DefaultVpcStack(app, nameof(PublishLambdaWithReferences), props), _defaultEnvironentResourceConfig);

            var webApp1 = builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithExternalHttpEndpoints();

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                    .WithReference(webApp1);

            await ExecuteApp(builder);
        }

        public static async Task PublishServerlessRedis()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishServerlessRedis));

            var cache = builder.AddRedis("Cache");

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("Service1", "../DeploymentTestApp.Service1")
                .WithReference(cache);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithReference(cache);

            await ExecuteApp(builder);
        }

        public static async Task PublishProvisionedRedis()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishProvisionedRedis));

            var cache = builder.AddRedis("Cache")
                               .PublishAsElasticCacheProvisionCluster();

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("Service1", "../DeploymentTestApp.Service1")
                .WithReference(cache);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithReference(cache);

            await ExecuteApp(builder);
        }

        public static async Task PublishServerlessValkey()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishServerlessValkey));

            var cache = builder.AddValkey("Cache");

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("Service1", "../DeploymentTestApp.Service1")
                .WithReference(cache);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithReference(cache);

            await ExecuteApp(builder);
        }

        public static async Task PublishProvisionedValkey()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishProvisionedValkey));

            var cache = builder.AddValkey("Cache")
                               .PublishAsElasticCacheProvisionCluster();

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("Service1", "../DeploymentTestApp.Service1")
                .WithReference(cache);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithReference(cache);

            await ExecuteApp(builder);
        }

        public static async Task PublishWithEnvironment()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWithEnvironment));

            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithEnvironment("MY_STATIC_VAR", "static-value")
                .WithEnvironment("ANOTHER_VAR", "another-value")
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishWithEnvironmentParameter()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());
            builder.Configuration["Parameters:api-key"] = "test-api-key-value";

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWithEnvironmentParameter));

            var apiKey = builder.AddParameter("api-key");

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithEnvironment("API_KEY", apiKey);

            await ExecuteApp(builder);
        }

        public static async Task PublishWithOptionalEnvironmentParameter()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());
            builder.Configuration["Parameters:required-key"] = "required-value";

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWithOptionalEnvironmentParameter));

            // optional-key has no configured value, which is valid because it is optional.
            var requiredKey = builder.AddParameter("required-key");
            var optionalKey = builder.AddParameter("optional-key").AsOptional();
            // Defaults defined by the AppHost. A secret's default must never be written to the template.
            var defaultedKey = builder.AddParameter("defaulted-key", "default-value", publishValueAsDefault: true);
            var optionalDefaultedKey = builder.AddParameter("optional-defaulted-key", "optional-default", publishValueAsDefault: true).AsOptional();
            var secretKey = builder.AddParameter("secret-key", "secret-value", secret: true);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithEnvironment("REQUIRED_KEY", requiredKey)
                .WithEnvironment("OPTIONAL_KEY", optionalKey)
                .WithEnvironment("DEFAULTED_KEY", defaultedKey)
                .WithEnvironment("OPTIONAL_DEFAULTED_KEY", optionalDefaultedKey)
                .WithEnvironment("SECRET_KEY", secretKey);

            await ExecuteApp(builder);
        }

        public static async Task PublishWithEnvironmentAndReference()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWithEnvironmentAndReference));

            var webApp1 = builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithExternalHttpEndpoints();

            builder.AddDotnetProject("WebApp2", "../DeploymentTestApps.WebApp2")
                .WithEnvironment("MY_CUSTOM_VAR", "custom-value")
                .WithReference(webApp1)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntime()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntime));

            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent");

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithMemory()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithMemory));

            // WithAgentCoreMemory() requests AgentCore memory, which should provision an AgentCore Memory resource,
            // point the agent's AWS_AGENTCORE_MEMORY_ID at it, and grant the runtime role memory access.
            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent")
                .WithAgentCoreMemory();

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithMultipleMemories()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithMultipleMemories));

            // Two agents each provision their own memory but share the default runtime role. The role
            // should receive a single bedrock-agentcore memory statement listing both memory ARNs, rather
            // than one statement per memory.
            builder.AddAgentCoreRuntime("AgentCoreAgent1", "../DeploymentTestApp.AgentCoreAgent")
                .WithAgentCoreMemory();

            builder.AddAgentCoreRuntime("AgentCoreAgent2", "../DeploymentTestApp.AgentCoreAgent")
                .WithAgentCoreMemory();

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithMemoryDisabled()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithMemoryDisabled));

            // WithAgentCoreMemory() is used for local testing, but CreateMemory = false suppresses provisioning the
            // memory resource during deployment.
            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent")
                .WithAgentCoreMemory()
                .PublishAsAgentCoreRuntime(new PublishAgentCoreRuntimeConfig
                {
                    CreateMemory = false
                });

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithCustomization()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithCustomization));

            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent")
                .PublishAsAgentCoreRuntime(new PublishAgentCoreRuntimeConfig
                {
                    PropsCfnRuntimeCallback = (ctx, props) =>
                    {
                        props.Description = "Custom agent description";
                    }
                });

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithReference()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithReference));

            var agent = builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent");

            // A consumer that references the agent should receive its runtime ARN as an environment
            // variable derived from the standard reference convention.
            builder.AddDotnetProject("WebApp1", "../DeploymentTestApps.WebApp1")
                .WithReference(agent)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithVpcReference()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithVpcReference));

            var cache = builder.AddRedis("Cache");

            // Referencing a VPC-requiring resource (ElastiCache) should switch the agent runtime's
            // network configuration to VPC mode.
            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent")
                .WithReference(cache);

            await ExecuteApp(builder);
        }

        public static async Task PublishAgentCoreRuntimeWithVpcReferenceAndSubnets()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishAgentCoreRuntimeWithVpcReferenceAndSubnets));

            var cache = builder.AddRedis("Cache");

            // The user pins explicit subnet IDs (e.g. to avoid AgentCore-unsupported availability zones).
            // These should take precedence over the default VPC's subnets.
            builder.AddAgentCoreRuntime("AgentCoreAgent", "../DeploymentTestApp.AgentCoreAgent")
                .WithReference(cache)
                .PublishAsAgentCoreRuntime(new PublishAgentCoreRuntimeConfig
                {
                    VpcSubnetIds = new[] { "subnet-aaaa1111", "subnet-bbbb2222" }
                });

            await ExecuteApp(builder);
        }

        /// <summary>
        /// When running the IDistributedApplication through tests for publishing there are exceptions thrown
        /// when the IDistributedApplication is shutting down. This method catches and ignores those exceptions to allow for clean test runs.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        private static async Task ExecuteApp(IDistributedApplicationBuilder builder)
        {
            try
            {
                await builder.Build().RunAsync();
            }
            catch (TaskCanceledException) { }
            catch (Microsoft.Extensions.Options.OptionsValidationException) { }
        }
    }
}
