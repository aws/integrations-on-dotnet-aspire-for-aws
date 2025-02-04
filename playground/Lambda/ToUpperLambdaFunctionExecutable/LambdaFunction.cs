using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

namespace ToUpperLambdaFunctionExecutable;

internal class LambdaFunction(TracerProvider traceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LambdaBootstrapBuilder.Create<string, string>(TracingLambdaHandler, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync(stoppingToken);
    }

    private string TracingLambdaHandler(string input, ILambdaContext context)
            => AWSLambdaWrapper.Trace(traceProvider, LambdaHandler, input, context);

    private string LambdaHandler(string input, ILambdaContext context)
    {
        return input.ToUpper();
    }
}
