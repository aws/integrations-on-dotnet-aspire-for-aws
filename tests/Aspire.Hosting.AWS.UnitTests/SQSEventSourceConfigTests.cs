using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class SQSEventSourceConfigTests
{
    [Fact]
    public void Minimal()
    {
        var config = SQSEventSourceResource.CreateSQSEventConfig("theQueueUrl", "theFunction", "theEmulatorUrl", null, null);
        Assert.Equal("QueueUrl=theQueueUrl,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl", config);
    }

    [Fact]
    public void WithOptions()
    {
        var config = SQSEventSourceResource.CreateSQSEventConfig("theQueueUrl", "theFunction", "theEmulatorUrl", new SQSEventSourceOptions { BatchSize = 10, DisableMessageDelete= true, VisibilityTimeout = 4}, null);
        Assert.Equal("QueueUrl=theQueueUrl,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,BatchSize=10,DisableMessageDelete=True,VisibilityTimeout=4", config);
    }

    [Fact]
    public void WithSDKProfile()
    {
        var config = SQSEventSourceResource.CreateSQSEventConfig("theQueueUrl", "theFunction", "theEmulatorUrl", null, new AWSSDKConfig { Profile = "beta", Region=Amazon.RegionEndpoint.USWest2});
        Assert.Equal("QueueUrl=theQueueUrl,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,Profile=beta,Region=us-west-2", config);
    }

    [Fact]
    public void WithOptionsAndProfile()
    {
        var config = SQSEventSourceResource.CreateSQSEventConfig("theQueueUrl", "theFunction", "theEmulatorUrl", new SQSEventSourceOptions { BatchSize = 10, }, new AWSSDKConfig { Profile = "beta" });
        Assert.Equal("QueueUrl=theQueueUrl,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,BatchSize=10,Profile=beta", config);
    }
}
