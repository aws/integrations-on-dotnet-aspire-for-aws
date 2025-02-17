using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WebCalculatorFunctions;

public class Functions
{
    IHost _host;
    TracerProvider _traceProvider;

    public Functions()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();
        _host = builder.Build();

        _traceProvider = _host.Services.GetRequiredService<TracerProvider>();
    }

    public APIGatewayHttpApiV2ProxyResponse AddFunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
            => AWSLambdaWrapper.Trace(_traceProvider, (request, context) =>
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
            }, request, context);

    public APIGatewayHttpApiV2ProxyResponse MinusFunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        => AWSLambdaWrapper.Trace(_traceProvider, (request, context) =>
        {
            var x = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            var y = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            var total = x - y;
            context.Logger.LogInformation($"Subtracting {y} from {x} equals {total}");
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json" }
            },
                Body = total.ToString()
            };

            return response;
        }, request, context);

    public APIGatewayHttpApiV2ProxyResponse MultiplyFunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        => AWSLambdaWrapper.Trace(_traceProvider, (request, context) =>
        {
            var x = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            var y = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            var total = x * y;
            context.Logger.LogInformation($"Multipling {y} with {x} equals {total}");
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json" }
            },
                Body = total.ToString()
            };

            return response;
        }, request, context);

    public APIGatewayHttpApiV2ProxyResponse DivideFunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        => AWSLambdaWrapper.Trace(_traceProvider, (request, context) =>
        {
            var x = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            var y = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            var total = x / (double)y;
            context.Logger.LogInformation($"Dividing {x} by {y} equals {total}");
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json" }
            },
                Body = total.ToString()
            };

            return response;
        }, request, context);
}
