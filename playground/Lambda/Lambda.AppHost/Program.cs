// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.AWS.Lambda;

#pragma warning disable CA2252 // This API requires opting into preview features

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSLambdaFunction<Projects.ToUpperLambdaFunctionExecutable>("ToUpperFunction", lambdaHandler: "ToUpperLambdaFunctionExecutable");

var defaultRouteLambda = builder.AddAWSLambdaFunction<Projects.WebDefaultLambdaFunction>("LambdaDefaultRoute", lambdaHandler: "WebDefaultLambdaFunction");
var addRouteLambda = builder.AddAWSLambdaFunction<Projects.WebAddLambdaFunction>("AddDefaultRoute", lambdaHandler: "WebAddLambdaFunction");
var minusRouteLambda = builder.AddAWSLambdaFunction<Projects.WebMinusLambdaFunction>("MinusDefaultRoute", lambdaHandler: "WebMinusLambdaFunction");
var listAwsResourcesRouteLambda = builder.AddAWSLambdaFunction<Projects.WebAWSCallsLambdaFunction>("ListAwsResourcesRoute", lambdaHandler: "WebAWSCallsLambdaFunction");

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", Aspire.Hosting.AWS.Lambda.APIGatewayType.HttpV2)
        .WithReference(defaultRouteLambda, Method.Get, "/")
        .WithReference(addRouteLambda, Method.Get, "/add/{x}/{y}")
        .WithReference(minusRouteLambda, Method.Get, "/minus/{x}/{y}")
        .WithReference(listAwsResourcesRouteLambda, Method.Get, "/aws/{service}");

builder.Build().Run();
 