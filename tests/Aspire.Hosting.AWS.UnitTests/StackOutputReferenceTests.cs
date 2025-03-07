// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CloudFormation.Model;
using Aspire.Hosting.AWS.CloudFormation;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class StackOutputReferenceTests
{
    [Fact]
    public async Task GetValueAsyncTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resourceBuilder = builder.AddAWSCloudFormationTemplate("NewStack", "cf.template");

        var resource = resourceBuilder.Resource as CloudFormationTemplateResource;
        Assert.NotNull(resource);

        resource.Outputs = new List<Output>
        {
            new Output{OutputKey = "key1", OutputValue = "value1"}
        };

        resource.ProvisioningTaskCompletionSource = new TaskCompletionSource();

        var reference = resourceBuilder.GetOutput("key1");
        Assert.Equal("key1", reference.Name);

        var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<TaskCanceledException>(() => reference.GetValueAsync(cancellationSource.Token).AsTask());

        resource.ProvisioningTaskCompletionSource.TrySetResult();
        var value = await reference.GetValueAsync(cancellationSource.Token);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void ValueExpressionTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resourceBuilder = builder.AddAWSCloudFormationTemplate("NewStack", "cf.template");

        var resource = resourceBuilder.Resource as CloudFormationTemplateResource;
        Assert.NotNull(resource);

        resource.Outputs = new List<Output>
        {
            new Output{OutputKey = "key1", OutputValue = "value1"}
        };

        var reference = resourceBuilder.GetOutput("key1");
        Assert.Equal("{NewStack.output.key1}", reference.ValueExpression);
    }

    [Fact]
    public void InvalidOutputKey()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resourceBuilder = builder.AddAWSCloudFormationTemplate("NewStack", "cf.template");

        var resource = resourceBuilder.Resource as CloudFormationTemplateResource;
        Assert.NotNull(resource);

        resource.Outputs = new List<Output>
        {
            new Output { OutputKey = "key1", OutputValue = "value1"}
        };

        var reference = resourceBuilder.GetOutput("not-found");
        Assert.Throws<System.InvalidOperationException>(() => reference.Value);
    }
}
