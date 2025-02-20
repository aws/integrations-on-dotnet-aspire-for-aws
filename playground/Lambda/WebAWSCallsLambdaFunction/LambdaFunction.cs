using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;
using System.Text.Json;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace WebAWSCallsLambdaFunction;

internal class LambdaFunction(TracerProvider traceProvider, IAmazonSQS sqsClient, IAmazonDynamoDB ddbClient, IAmazonSecurityTokenService stsClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LambdaBootstrapBuilder.Create<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(TracingLambdaHandler, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync(stoppingToken);
    }

    private Task<APIGatewayHttpApiV2ProxyResponse> TracingLambdaHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
            => AWSLambdaWrapper.TraceAsync(traceProvider, LambdaHandler, request, context);

    private async Task<APIGatewayHttpApiV2ProxyResponse> LambdaHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)    
    {
        var service = request.PathParameters["service"];
        context.Logger.LogInformation("List resources for service: {service}", service);

        List<string>? resources = null;
        switch(service?.ToUpper())
        {
            case "SQS":
                var sqsResponse = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
                resources = sqsResponse.QueueUrls;
                break;
            case "DYNAMODB":
                var ddbResponse = await ddbClient.ListTablesAsync();
                resources = ddbResponse.TableNames;
                break;
            case "STS":
                var iamResponse = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
                resources = new(){ "Found caller identity" };
                break;
        }

        if (resources == null)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 404,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "text/plain" }
                },
                Body = $"Service {service} not found"
            };
        }

        var response = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json" }
            },
            Body = JsonSerializer.Serialize(resources)
        };

        return response;
    }
}
