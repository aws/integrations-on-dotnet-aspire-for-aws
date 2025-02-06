using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

namespace WebAddLambdaFunction;

internal class LambdaFunction(TracerProvider traceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LambdaBootstrapBuilder.Create<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(TracingLambdaHandler, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync(stoppingToken);
    }

    private APIGatewayHttpApiV2ProxyResponse TracingLambdaHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
            => AWSLambdaWrapper.Trace(traceProvider, LambdaHandler, request, context);

    private APIGatewayHttpApiV2ProxyResponse LambdaHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var x = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
        var y = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
        var sum = x + y;
        context.Logger.LogInformation($"Adding {x} with {y} is {sum}");
        var response = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json" }
            },
            Body = sum.ToString()
        };

        return response;
    }
}
