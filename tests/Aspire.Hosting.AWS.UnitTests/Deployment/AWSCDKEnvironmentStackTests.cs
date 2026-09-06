// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.SQS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.Deployment;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

public class AWSCDKEnvironmentStackTests
{
    [Fact]
    public void UseDeploymentStackCreatesCompanionAndRegistersItForRunMode()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddAWSCDKEnvironment("environment", CDKDefaultsProviderFactory.Preview_V1);

        var stack = environment.UseDeploymentStack();
        var sameStack = environment.UseDeploymentStack();
        var queue = stack.AddSQSQueue("queue");

        Assert.Same(stack.Resource, sameStack.Resource);
        Assert.NotSame(environment.Resource, stack.Resource);
        Assert.Same(environment.Resource.CDKStack, stack.Resource.Stack);
        Assert.Equal("environment-stack", stack.Resource.Name);
        Assert.Equal("environment-local", stack.Resource.StackName);
        Assert.Same(environment.Resource, Assert.IsAssignableFrom<IResourceWithParent>(stack.Resource).Parent);
        Assert.Same(stack.Resource, queue.Resource.Parent);
        Assert.DoesNotContain(builder.Resources, resource => ReferenceEquals(resource, environment.Resource));
        Assert.Single(builder.Resources.OfType<IStackResource>());
    }

    [Fact]
    public void UseDeploymentStackCreatesCompanionInPublishMode()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });
        var environment = builder.AddAWSCDKEnvironment("environment", CDKDefaultsProviderFactory.Preview_V1);

        var stack = environment.UseDeploymentStack();
        var queue = stack.AddConstruct("queue", scope => new Queue(scope, "queue"));

        Assert.NotSame(environment.Resource, stack.Resource);
        Assert.Same(environment.Resource.CDKStack, stack.Resource.Stack);
        Assert.Same(stack.Resource, queue.Resource.Parent);
        Assert.Contains(builder.Resources, resource => ReferenceEquals(resource, environment.Resource));
        Assert.Single(builder.Resources.OfType<IStackResource>());
        Assert.Equal("environment", stack.Resource.StackName);
    }

    [Fact]
    public void UseDeploymentStackSupportsCustomRunStackName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var environment = builder.AddAWSCDKEnvironment("environment", CDKDefaultsProviderFactory.Preview_V1);

        var stack = environment.UseDeploymentStack("developer-infrastructure");

        Assert.Equal("developer-infrastructure", stack.Resource.StackName);
    }
}
