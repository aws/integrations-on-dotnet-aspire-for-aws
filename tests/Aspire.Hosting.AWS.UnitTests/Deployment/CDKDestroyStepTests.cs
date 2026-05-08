// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.Pipelines;
using Constructs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class CDKDestroyStepTests
{


    [Fact]
    public void CDKDestroyStep_IsRegistered_InServiceCollection()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });

        // Act
        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);
        var services = builder.Services.BuildServiceProvider();

        // Assert
        var destroyStep = services.GetService<CDKDestroyStep>();
        Assert.NotNull(destroyStep);
    }

    [Fact]
    public void CDKDestroyStep_IsRegistered_AsSingleton()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });
        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);
        var services = builder.Services.BuildServiceProvider();

        // Act
        var instance1 = services.GetService<CDKDestroyStep>();
        var instance2 = services.GetService<CDKDestroyStep>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void MultipleEnvironments_EachHave_DestroyAnnotation()
    {
        // Arrange
        var app1 = new App();
        var stack1 = new TestStack(app1, "Stack1", null);
        var env1 = new AWSCDKEnvironmentResource<Stack>(
            "env1",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack1,
            null);

        var app2 = new App();
        var stack2 = new TestStack(app2, "Stack2", null);
        var env2 = new AWSCDKEnvironmentResource<Stack>(
            "env2",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack2,
            null);

        // Act
        var annotations1 = env1.Annotations.OfType<PipelineStepAnnotation>().ToList();
        var annotations2 = env2.Annotations.OfType<PipelineStepAnnotation>().ToList();

        // Assert - each environment should have its own set of 3 pipeline annotations
        Assert.Equal(3, annotations1.Count);
        Assert.Equal(3, annotations2.Count);
    }

    [Fact]
    public void DestroyAnnotation_IsDistinct_FromPublishAndDeployAnnotations()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack", null);
        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        // Act
        var pipelineAnnotations = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .ToList();

        // Assert - all three annotations should be distinct instances
        Assert.Equal(3, pipelineAnnotations.Distinct().Count());
    }

    [Fact]
    public void CDKDestroyStep_CanBeResolved_WithDependencies()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });
        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);
        var services = builder.Services.BuildServiceProvider();

        // Act & Assert - CDKDestroyStep should resolve without throwing
        var destroyStep = services.GetRequiredService<CDKDestroyStep>();
        Assert.NotNull(destroyStep);
    }

    private class TestStack : Stack
    {
        public TestStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }
}
