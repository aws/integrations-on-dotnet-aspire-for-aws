using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

namespace WebDefaultLambdaFunction;

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
        context.Logger.LogInformation($"Hit default route");
        var response = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
        {
            {"Content-Type", "text/plain" }
        },
            Body = "This is the REST API calculator. Try using endpoints /add/1/2 and /minus/3/2"
        };

        return response;
    }
}
