using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;
using StackExchange.Redis;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DeploymentTestApp.LambdaFunction1;

public class Function
{
    IHost _host;
    TracerProvider _traceProvider;
    IDatabase _db;

    public Function()
    {
        var builder = new HostApplicationBuilder();
        builder.AddRedisClient("cache");
        builder.AddServiceDefaults();
        _host = builder.Build();

        _traceProvider = _host.Services.GetRequiredService<TracerProvider>();

        _db = _host.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
    }

    public Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(_traceProvider, async (evnt, context) =>
        {
            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
            var processedMessages = await _db.StringIncrementAsync("messagesProcessed", evnt.Records.Count);
            context.Logger.LogInformation("Total messages processed: {count}", processedMessages);

        }, evnt, context);

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processed message {message.Body}");

        // TODO: Do interesting work based on the new message
        await Task.CompletedTask;
    }
}