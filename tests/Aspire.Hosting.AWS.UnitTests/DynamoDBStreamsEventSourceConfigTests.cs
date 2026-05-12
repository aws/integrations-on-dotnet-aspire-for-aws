using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class DynamoDBStreamsEventSourceConfigTests
{
    [Fact]
    public void Minimal()
    {
        var config = DynamoDBStreamsEventSourceResource.CreateDynamoDBStreamsEventConfig("theTable", "theFunction", "theEmulatorUrl", null, null);
        Assert.Equal("TableName=theTable,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl", config);
    }

    [Fact]
    public void WithOptions()
    {
        var config = DynamoDBStreamsEventSourceResource.CreateDynamoDBStreamsEventConfig("theTable", "theFunction", "theEmulatorUrl", new DynamoDBStreamsEventSourceOptions { BatchSize = 10 }, null);
        Assert.Equal("TableName=theTable,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,BatchSize=10", config);
    }

    [Fact]
    public void WithSDKProfile()
    {
        var config = DynamoDBStreamsEventSourceResource.CreateDynamoDBStreamsEventConfig("theTable", "theFunction", "theEmulatorUrl", null, new AWSSDKConfig { Profile = "beta", Region = Amazon.RegionEndpoint.USWest2 });
        Assert.Equal("TableName=theTable,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,Profile=beta,Region=us-west-2", config);
    }

    [Fact]
    public void WithOptionsAndProfile()
    {
        var config = DynamoDBStreamsEventSourceResource.CreateDynamoDBStreamsEventConfig("theTable", "theFunction", "theEmulatorUrl", new DynamoDBStreamsEventSourceOptions { BatchSize = 10 }, new AWSSDKConfig { Profile = "beta" });
        Assert.Equal("TableName=theTable,FunctionName=theFunction,LambdaRuntimeApi=theEmulatorUrl,BatchSize=10,Profile=beta", config);
    }
}
