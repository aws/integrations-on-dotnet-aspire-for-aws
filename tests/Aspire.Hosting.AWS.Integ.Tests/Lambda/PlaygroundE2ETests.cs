using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Aspire.Hosting.AWS.Lambda;

namespace Aspire.Hosting.AWS.Integ.Tests.Lambda;

public class PlaygroundE2ETests
{
    [Fact]
    public async Task RunAWSAppHostProject()
    {
        var cancellationToken = new CancellationTokenSource();
        cancellationToken.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Lambda_AppHost>();
            await using var app = await appHost.BuildAsync();
            await app.StartAsync();

            var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
            await resourceNotificationService
                .WaitForResourceAsync("LambdaServiceEmulator", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(120));
            await resourceNotificationService
                .WaitForResourceAsync("APIGatewayEmulator", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(120));
            // await resourceNotificationService
            //     .WaitForResourceAsync("AddFunction", KnownResourceStates.Running)
            //     .WaitAsync(TimeSpan.FromSeconds(120));

            var lambdaServiceEmulator = (LambdaEmulatorResource)appHost.Resources
                                .Single(static r => r.Name == "LambdaServiceEmulator");
            var lambdaEmulatorAnnotation = lambdaServiceEmulator.Annotations.OfType<LambdaEmulatorAnnotation>().Single();

            var apiGatewayEmulator = (APIGatewayEmulatorResource)appHost.Resources
                                .Single(static r => r.Name == "APIGatewayEmulator");
            var apiGatewayEmulatorAnnotation = apiGatewayEmulator.Annotations.OfType<APIGatewayEmulatorAnnotation>().Single();
            var apiGatewayEmulatorEndpointAnnotation = apiGatewayEmulator.Annotations.OfType<EndpointAnnotation>().Single();

            Assert.Equal("The root page for the REST API defined in the Aspire AppHost. Try using endpoints /add/{1}/2, /minus/3/2, /multiply/6/7, /divide/20/4 or /aws/{sqs|dynamodb}",
                await TestEndpoint("/", app, "APIGatewayEmulator"));
            Assert.Equal("[\"Found caller identity\"]", await TestEndpoint("/aws/STS", app, "APIGatewayEmulator"));
            
            var client = CreateLambdaServiceClient(lambdaEmulatorAnnotation.Endpoint.Url);
            Assert.Equal("3",
                await TestLambda("AddFunction", "1", "2", client));
            Assert.Equal("1",
                await TestLambda("MinusFunction", "2", "1", client));
            Assert.Equal("2",
                await TestLambda("MultiplyFunction", "2", "1", client));
            Assert.Equal("2",
                await TestLambda("DivideFunction", "2", "1", client));
        }
        finally
        {
            await cancellationToken.CancelAsync();
        }
    }

    private async Task<string> TestLambda(string functionName, string x, string y, IAmazonLambda client)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var payload = JsonSerializer.Serialize(new APIGatewayHttpApiV2ProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                {"x", x},
                {"y", y}
            }
        }, options);

        var invokeRequest = new InvokeRequest
        {
            FunctionName = functionName,
            InvocationType = InvocationType.RequestResponse,
            Payload = payload
        };

        var response = await client.InvokeAsync(invokeRequest);
        APIGatewayHttpApiV2ProxyResponse? lambdaResponse;
        using (var reader = new StreamReader(response.Payload))
        {
            var responseJson = await reader.ReadToEndAsync();

            lambdaResponse = JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(responseJson, options);
        }
        
        return lambdaResponse?.Body ?? string.Empty;
    }

    private async Task<string> TestEndpoint(string routeName, DistributedApplication app, string resourceName, int requestTimeout = 30, int totalTimeout = 200)
    {
        using (var client = app.CreateHttpClient(resourceName))
        {
            client.Timeout = TimeSpan.FromSeconds(requestTimeout);
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(totalTimeout);
            Exception? lastException = null;

            while (DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(1_000);

                try
                {
                    var response = await client.GetAsync(routeName);
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(500);
                }
            }

            throw new TimeoutException($"Failed to complete request within timeout period: {lastException?.Message}", lastException);
        }
    }
    
    private static IAmazonLambda CreateLambdaServiceClient(string endpoint)
    {
        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = endpoint
        };

        return new AmazonLambdaClient(new Amazon.Runtime.BasicAWSCredentials("accessKey", "secretKey"), lambdaConfig);
    }
}
