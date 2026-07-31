// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.AWS.Lambda;

var builder = DistributedApplication.CreateBuilder(args);

// Configure AWS SDK (region / credentials) used by the Lambda function and DynamoDB Local.
var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

// Start a DynamoDB Local container so the Python Lambda can persist its invocation counter.
var dynamoDBLocal = builder.AddAWSDynamoDBLocal("DynamoDBLocal");

// Register the Python Lambda function.
// Aspire will run: python -m awslambdaric (with _HANDLER=main.handler set in the environment)
var toUpperFunction = builder.AddAWSPythonLambdaFunction(
        name: "ToUpperFunction",
        appDirectory: "../ToUpperLambda",
        handler: "main.handler")
    .WithReference(awsSdkConfig)
    .WithReference(dynamoDBLocal)
    .WithEnvironment("COUNTER_TABLE_NAME", "InvocationCounters");

// Front the Lambda with the API Gateway emulator.
// After `aspire run`, POST http://localhost:<port>/to-upper with a plain text body.
builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(toUpperFunction, Method.Post, "/to-upper");

builder.Build().Run();
