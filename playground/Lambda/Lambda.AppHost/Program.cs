// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
using Amazon.CDK.AWS.DynamoDB;
using Amazon.Lambda;
using Aspire.Hosting.AWS.Lambda;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSLambdaServiceEmulator(new LambdaEmulatorOptions
{
    DisableAutoInstall = true
});

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
cdkStackResource.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

var sqsDemoQueue1 = cdkStackResource.AddSQSQueue("DemoQueue1");
var sqsDemoQueue2 = cdkStackResource.AddSQSQueue("DemoQueue2");

var table = cdkStackResource.AddDynamoDBTable("DemoTable", new TableProps
{
    PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "Id", Type = AttributeType.STRING },
    Stream = StreamViewType.NEW_AND_OLD_IMAGES,
    BillingMode = BillingMode.PAY_PER_REQUEST
});

builder.AddAWSLambdaFunction<Projects.ToUpperLambdaFunctionExecutable>("ToUpperFunction", lambdaHandler: "ToUpperLambdaFunctionExecutable", new LambdaFunctionOptions { ApplicationLogLevel = ApplicationLogLevel.DEBUG, LogFormat = LogFormat.JSON });

var defaultRouteLambda = builder.AddAWSLambdaFunction<Projects.WebDefaultLambdaFunction>("LambdaDefaultRoute", lambdaHandler: "WebDefaultLambdaFunction");
var listAwsResourcesRouteLambda = builder.AddAWSLambdaFunction<Projects.WebAWSCallsLambdaFunction>("ListAwsResourcesRoute", lambdaHandler: "WebAWSCallsLambdaFunction");

var addFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("AddFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::AddFunctionHandler");
var minusFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("MinusFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::MinusFunctionHandler");
var multiplyFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("MultiplyFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::MultiplyFunctionHandler");
var divideFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("DivideFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::DivideFunctionHandler");

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", Aspire.Hosting.AWS.Lambda.APIGatewayType.HttpV2)
        .WithReference(defaultRouteLambda, Method.Get, "/")
        // Add route demonstrating making AWS service calls
        .WithReference(listAwsResourcesRouteLambda, Method.Get, "/aws/{service}")
        // Add the Web API calculator routes
        .WithReference(addFunction, Method.Get, "/add/{x}/{y}")
        .WithReference(minusFunction, Method.Get, "/minus/{x}/{y}")
        .WithReference(multiplyFunction, Method.Get, "/multiply/{x}/{y}")
        .WithReference(divideFunction, Method.Get, "/divide/{x}/{y}");


builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .WithReference(awsSdkConfig)
        .WithSQSEventSource(sqsDemoQueue1)
        // These references are not necessary. It is added to confirm duplicate
        // CDK output parameters are not attempted to be added.
        .WithReference(sqsDemoQueue1)
        .WithReference(sqsDemoQueue2);


builder.AddAWSLambdaFunction<Projects.DynamoDBProcessorFunction>("DynamoDBProcessorFunction", "DynamoDBProcessorFunction::DynamoDBProcessorFunction.Function::FunctionHandler")
        .WithDynamoDBStreamsEventSource(table, new DynamoDBStreamsEventSourceOptions
        {
            BatchSize = 5
        })
        .WithDynamoDBStreamsEventSource("StreamingTest");


builder.Build().Run();
 