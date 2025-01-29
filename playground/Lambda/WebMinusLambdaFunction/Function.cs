using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
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
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();