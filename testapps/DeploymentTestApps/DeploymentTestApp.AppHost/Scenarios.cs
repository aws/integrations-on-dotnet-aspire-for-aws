using Amazon.CDK;
using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Deployment;

using Environment = System.Environment;

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

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

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .PublishAsECSFargateExpressService(new PublishECSFargateExpressServiceConfig
                {
                    PropsCfnExpressGatewayServicePropsCallback = (context, props) =>
                    {
                        props.Memory = "4096";
                    }
                })
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApps_WebApp2>("WebApp2")
                .WithReference(webApp1)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishWebApp2ReferenceOnWebApp1WithAlb()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishWebApp2ReferenceOnWebApp1WithAlb));

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .PublishAsECSFargateServiceWithALB(new PublishECSFargateServiceWithALBConfig
                {
                    PropsApplicationLoadBalancedFargateServiceCallback = (context, props) =>
                    {
                        props.MemoryLimitMiB = 4096;
                    }
                })
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApps_WebApp2>("WebApp2")
                .PublishAsECSFargateServiceWithALB()
                .WithReference(webApp1)
                .WithExternalHttpEndpoints();

            await ExecuteApp(builder);
        }

        public static async Task PublishService1ReferenceOnWebApp1()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, _defaultEnvironentResourceConfig, nameof(PublishService1ReferenceOnWebApp1));

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
                .WithReference(webApp1);

            await ExecuteApp(builder);
        }

        public static async Task PublishWebApp1UsingDefaultVpc()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new DefaultVpcStack(app, nameof(PublishWebApp1UsingDefaultVpc), props), _defaultEnvironentResourceConfig);

            builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
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

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
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

            builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
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

            builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
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

            builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
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

            builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithReference(cache)
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
                .WithReference(cache);

            builder.AddAWSLambdaFunction<Projects.DeploymentTestApp_LambdaFunction1>("LambdaFunction1", "DeploymentTestApp.LambdaFunction1::DeploymentTestApp.LambdaFunction1.Function::FunctionHandler")
                .WithReference(cache);

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
