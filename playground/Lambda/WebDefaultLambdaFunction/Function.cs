using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
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
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();