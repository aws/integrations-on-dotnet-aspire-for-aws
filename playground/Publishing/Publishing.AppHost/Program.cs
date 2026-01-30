// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Deployment;
using Lambda.AppHost;

// TODOs:
// Support Publish methods from AddContainer
// Handle the AWS application resources provisioned by the AddAWSCDKStack so they are included in the deployment
// Provisioning RDS databases and connecting via WithReference
// Projects deployed to Beanstalk
// Parameter hints, Having the AddParameter hint that the parameter is something like a VPC
// Enabling OTEL collection to CloudWatch

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

builder.AddAWSCDKEnvironment("aws", 
                                    CDKDefaultsProviderFactory.Preview_V1, 
                                    (app, props) => new DeploymentStack(app, "AspirePlay1", props), 
                                    new AWSCDKEnvironmentResourceConfig { AWSSDKConfig = awsSdkConfig });

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

var cache = builder.AddValkey("cache");

var frontend = builder.AddProject<Projects.Frontend>("Frontend")
        .WithExternalHttpEndpoints()
        .WithReference(cache)
        .WaitFor(cache);


builder.AddProject<Projects.Backend>("backend")
        .WithReference(frontend)
        .WithReference(cache)
        .WaitFor(cache);

builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .PublishAsLambdaFunction(new PublishLambdaFunctionConfig
        {
            ConstructFunctionCallback = (ctx, construct) =>
            {
                construct.AddEventSource(new SqsEventSource(ctx.GetDeploymentStack<DeploymentStack>().LambdaQueue, new SqsEventSourceProps
                {
                    BatchSize = 5,
                    Enabled = true
                }));
            }
        })
        .WithReference(awsSdkConfig)
        .WithSQSEventSource(localDevQueue);

builder.Build().Run();
 