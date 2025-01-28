using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
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
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();