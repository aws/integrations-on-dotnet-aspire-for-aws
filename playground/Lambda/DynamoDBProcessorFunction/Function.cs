using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DynamoDBProcessorFunction;

public class Function
{
    IHost _host;
    TracerProvider _traceProvider;
    long _processedRecords = 0;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();
        _host = builder.Build();

        _traceProvider = _host.Services.GetRequiredService<TracerProvider>();
    }

    public Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
        => AWSLambdaWrapper.TraceAsync(_traceProvider, async (dynamoEvent, context) =>
    {
        context.Logger.LogInformation($"Beginning to process {dynamoEvent.Records.Count} records...");

        foreach (var record in dynamoEvent.Records)
        {
            _processedRecords++;

            context.Logger.LogInformation($"Event ID: {record.EventID}");
            context.Logger.LogInformation($"Event Name: {record.EventName}");

            // TODO: Add business logic processing the record.Dynamodb object.
        }

        context.Logger.LogInformation("Stream processing complete. Total events processed {ProcessCount}", _processedRecords);

        return Task.CompletedTask;
    }, dynamoEvent, context);
}