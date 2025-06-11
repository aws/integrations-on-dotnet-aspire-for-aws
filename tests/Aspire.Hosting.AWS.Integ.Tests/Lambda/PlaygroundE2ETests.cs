using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.Lambda;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;

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
            await resourceNotificationService
                .WaitForResourceAsync("AWSLambdaPlaygroundResources", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(120));
            await resourceNotificationService
                .WaitForResourceAsync("SQSEventSource-SQSProcessorFunction", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(120));

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
            Assert.Equal("3",
                await TestEndpoint("/add/1/2", app, "APIGatewayEmulator"));
            Assert.Equal("1",
                await TestEndpoint("/minus/2/1", app, "APIGatewayEmulator"));
            Assert.Equal("2",
                await TestEndpoint("/multiply/2/1", app, "APIGatewayEmulator"));
            Assert.Equal("2",
                await TestEndpoint("/divide/2/1", app, "APIGatewayEmulator"));

            var stackResource = (IStackResource)appHost.Resources
                                .Single(static r => r.Name == "AWSLambdaPlaygroundResources");

            var queueUrl = stackResource.Outputs?.FirstOrDefault(x =>
            {
                // The output key has a hash in the middle which could change. To avoid hardcoded logic on the hash check the start and end.
                // Example value of the output key "DemoQueue955156E8QueueUrl".
                return x.OutputKey.StartsWith("DemoQueue1") && x.OutputKey.EndsWith("QueueUrl");
            })?.OutputValue;
            
            Assert.NotNull(queueUrl);

            using var sqsClient = new AmazonSQSClient(RegionEndpoint.USWest2);
            await sqsClient.SendMessageAsync(queueUrl, "themessage", cancellationToken.Token);

            // Wait for the Lambda function to consume the message it gets deleted.
            await Task.Delay(20000);
            var queueAttributesRepsonse = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest { QueueUrl = queueUrl, AttributeNames = new List<string> { "All" } });
            Assert.Equal(0, queueAttributesRepsonse.ApproximateNumberOfMessages + queueAttributesRepsonse.ApproximateNumberOfMessagesNotVisible);
        }
        finally
        {
            await cancellationToken.CancelAsync();
        }
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
}
