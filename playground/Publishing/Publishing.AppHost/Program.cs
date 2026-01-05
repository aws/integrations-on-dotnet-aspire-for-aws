// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Deployment;
using Lambda.AppHost;
using System.Diagnostics;

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

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var awsEnvironment = builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1, (app, props) => new DeploymentStack(app, "DeploymentInfrastructure18", props))
                            .WithReference(awsSdkConfig);

var deploymentStack = awsEnvironment.Resource.EnvironmentStack;
var deploymentTag = "v" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");


//var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
//var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

var cache = builder.AddRedis("cache");

//var frontend = builder.AddProject<Projects.Frontend>("Frontend")
//        .WithExternalHttpEndpoints()
//        .WithDeploymentImageTag(context => deploymentTag)
//        .WithReference(cache)
//        .WaitFor(cache);


//builder.AddProject<Projects.Backend>("backend")
//        .WithDeploymentImageTag(context => deploymentTag)
//        .WithReference(frontend)
//        .WithReference(cache)
//        .WaitFor(cache);

//builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
//        .WithDeploymentImageTag(context => deploymentTag)
//        .PublishAsLambdaFunction(new PublishLambdaFunctionConfig
//        {
//            ConstructFunctionCallback = construct =>
//            {
//                construct.AddEventSource(new SqsEventSource(deploymentStack.LambdaQueue, new SqsEventSourceProps
//                {
//                    BatchSize = 5,
//                    Enabled = true
//                }));
//            }
//        })
//        .WithReference(cache)
//        .WithReference(awsSdkConfig)
//        .WithSQSEventSource(localDevQueue);

builder.Build().Run();
 