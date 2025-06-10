// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Lambda.AppHost;
using Amazon.CDK.AWS.Lambda.EventSources;

#pragma warning disable CA2252 // This API requires opting into preview features

var builder = DistributedApplication.CreateBuilder(args);

var awsDeployment = builder.AddAWSCDKEnvironment("aws", app => new DeploymentStack(app, "DeploymentInfrastructure"));

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

builder.AddProject<Projects.Frontend>("Frontend");


builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .WithPublishingCDKPropsCallback(props =>
        {
            props.Vpc = awsDeployment.Resource.EnvironmentStack.MainVpc;
            props.MemorySize = 512;
        })
        .WithPublishingCDKConstructCallback(construct =>
        {
            construct.AddEventSource(new SqsEventSource(awsDeployment.Resource.EnvironmentStack.LambdaQueue, new SqsEventSourceProps
            {
                BatchSize = 5,
                Enabled = true
            }));
        })
        .WithReference(awsSdkConfig)
        .WithSQSEventSource(localDevQueue);


builder.Build().Run();
 