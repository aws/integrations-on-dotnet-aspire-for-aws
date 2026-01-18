// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Constructs;
using Xunit;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

public class ApplyDefaultsTests
{
    [Fact]
    public void ApplyCfnExpressGatewayServiceDefaults_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new CfnExpressGatewayServiceProps
        {
            PrimaryContainer = new ExpressGatewayContainerProperty()
        };

        // Act
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(props);

        // Assert
        Assert.NotNull(props.Cluster);
        Assert.Equal("1024", props.Cpu);
        Assert.Equal("2048", props.Memory);
        Assert.NotNull(props.ExecutionRoleArn);
        Assert.NotNull(props.InfrastructureRoleArn);

        var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
        Assert.NotNull(primaryContainer);
        Assert.Equal(8080, primaryContainer.ContainerPort);

        var expectedClusterName = environment.DefaultsProvider.GetDefaultECSCluster().ClusterName;
        Assert.Equal(expectedClusterName, props.Cluster);

        var expectedExecutionRoleArn = environment.DefaultsProvider.GetDefaultECSExpressExecutionRole().RoleArn;
        Assert.Equal(expectedExecutionRoleArn, props.ExecutionRoleArn);

        var expectedInfrastructureRoleArn = environment.DefaultsProvider.GetDefaultECSExpressInfrastructureRole().RoleArn;
        Assert.Equal(expectedInfrastructureRoleArn, props.InfrastructureRoleArn);
    }

    [Fact]
    public void ApplyCfnExpressGatewayServiceDefaults_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingCluster = "my-existing-cluster";
        var existingCpu = "512";
        var existingMemory = "1024";
        var existingPort = 3000;
        var existingExecutionRoleArn = "arn:aws:iam::123456789012:role/my-execution-role";
        var existingInfrastructureRoleArn = "arn:aws:iam::123456789012:role/my-infrastructure-role";

        var primaryContainer = new ExpressGatewayContainerProperty
        {
            ContainerPort = existingPort
        };

        var props = new CfnExpressGatewayServiceProps
        {
            Memory = existingMemory,
            Cpu = existingCpu,
            Cluster = existingCluster,
            PrimaryContainer = primaryContainer,
            ExecutionRoleArn = existingExecutionRoleArn,
            InfrastructureRoleArn = existingInfrastructureRoleArn,
        };

        // Act
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(props);

        // Assert
        Assert.Equal(existingCluster, props.Cluster);
        Assert.Equal(existingCpu, props.Cpu);
        Assert.Equal(existingMemory, props.Memory);
        Assert.Equal(existingPort, primaryContainer!.ContainerPort);
        Assert.Equal(existingExecutionRoleArn, props.ExecutionRoleArn);
        Assert.Equal(existingInfrastructureRoleArn, props.InfrastructureRoleArn);
    }


    // Helper method to create provider and environment
    private static AWSCDKEnvironmentResource<Stack> CreateProviderAndEnvironment()
    {
        var app = new App();
        var stack = new Stack(app, "TestStack");

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        return environment;
    }
}
