#pragma warning disable ASPIREJAVASCRIPT001
#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSCDKEnvironment(
    name: "spa-and-api",
    cdkDefaultsProviderFactory: Aspire.Hosting.AWS.Deployment.CDKDefaultsProviderFactory.Preview_V1,
    environmentResourceConfig: new Aspire.Hosting.AWS.Deployment.AWSCDKEnvironmentResourceConfig
    {
        AWSSDKConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUCentral1)
    })
    ;

var backend = builder.AddProject<Projects.Backend>("Backend")
    .WithExternalHttpEndpoints()
    .PublishAsECSFargateServiceWithALB(new Aspire.Hosting.AWS.Deployment.PublishECSFargateServiceWithALBConfig
    {
        PropsApplicationLoadBalancedFargateServiceCallback = (_, props) => props.DesiredCount = 1
    })
    ;

var frontend = builder.AddViteApp("Frontend", "../Frontend")
    .WithEndpoint("http", endpointAnnotation =>
    {
        endpointAnnotation.Port = 3000;
    })
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WithCloudFrontBackendBehavior("/todos/*", backend)
    .PublishAsS3WithCloudFront(config =>
    {
        config.OutputPath = "dist/Frontend/browser";
    })
    ;

builder.Build().Run();
