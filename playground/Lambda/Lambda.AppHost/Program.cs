// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
using Amazon.Lambda;
using Aspire.Hosting;
using Aspire.Hosting.AWS.Lambda;

#pragma warning disable CA2252 // This API requires opting into preview features

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var sqsDemoQueue1 = cdkStackResource.AddSQSQueue("DemoQueue1");
var sqsDemoQueue2 = cdkStackResource.AddSQSQueue("DemoQueue2");

builder.AddAWSLambdaFunction<Projects.ToUpperLambdaFunctionExecutable>("ToUpperFunction", lambdaHandler: "ToUpperLambdaFunctionExecutable", new LambdaFunctionOptions { ApplicationLogLevel = ApplicationLogLevel.DEBUG, LogFormat = LogFormat.JSON});

var defaultRouteLambda = builder.AddAWSLambdaFunction<Projects.WebDefaultLambdaFunction>("LambdaDefaultRoute", lambdaHandler: "WebDefaultLambdaFunction");
var listAwsResourcesRouteLambda = builder.AddAWSLambdaFunction<Projects.WebAWSCallsLambdaFunction>("ListAwsResourcesRoute", lambdaHandler: "WebAWSCallsLambdaFunction");

var addFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("AddFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::AddFunctionHandler");
var minusFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("MinusFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::MinusFunctionHandler");
var multiplyFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("MultiplyFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::MultiplyFunctionHandler");
var divideFunction = builder.AddAWSLambdaFunction<Projects.WebCalculatorFunctions>("DivideFunction", lambdaHandler: "WebCalculatorFunctions::WebCalculatorFunctions.Functions::DivideFunctionHandler");

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", Aspire.Hosting.AWS.Lambda.APIGatewayType.HttpV2)
        .WithReference(defaultRouteLambda, Method.Get, "/")
        // Add route demonstrating making AWS servic calls
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


builder.Build().Run();
 