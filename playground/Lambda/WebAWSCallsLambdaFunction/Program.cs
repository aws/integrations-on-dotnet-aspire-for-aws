using WebAWSCallsLambdaFunction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();
builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
builder.Services.AddAWSService<Amazon.SQS.IAmazonSQS>();
builder.Services.AddAWSService<Amazon.SecurityToken.IAmazonSecurityTokenService>();
builder.Services.AddHostedService<LambdaFunction>();

builder.Build().Run();