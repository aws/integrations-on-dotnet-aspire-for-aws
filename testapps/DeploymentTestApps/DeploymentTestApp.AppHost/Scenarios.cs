using Aspire.Hosting;
using Aspire.Hosting.AWS.Deployment;

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

namespace DeploymentTestApp.AppHost
{
    public static class Scenarios
    {

        public static async Task PublishWebApp2ReferenceOnWebApp1()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, stackName: nameof(PublishWebApp2ReferenceOnWebApp1));

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

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, stackName: nameof(PublishWebApp2ReferenceOnWebApp1WithAlb));

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

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, stackName: nameof(PublishService1ReferenceOnWebApp1));

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithExternalHttpEndpoints();

            builder.AddProject<Projects.DeploymentTestApp_Service1>("Service1")
                .WithReference(webApp1);

            await ExecuteApp(builder);
        }

        public static async Task PublishWebApp1UsingDefaultVpc()
        {
            var builder = DistributedApplication.CreateBuilder(Environment.GetCommandLineArgs());

            builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new DefaultVpcStack(app, nameof(PublishWebApp1UsingDefaultVpc), props),
                new AWSCDKEnvironmentResourceConfig
                {
                    OverrideAppHostAssemblyName = "DeploymentTestApp.AppHost.dll"
                });

            var webApp1 = builder.AddProject<Projects.DeploymentTestApps_WebApp1>("WebApp1")
                .WithExternalHttpEndpoints();

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
