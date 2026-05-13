// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 3: Missing required parameter produces error

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Constructs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Property 3: Missing required parameter produces error
/// 
/// For any CfnParameter without a default value, if no corresponding value exists in
/// IConfiguration at the derived key, the resolution method SHALL throw an exception
/// whose message contains the parameter name and the expected configuration key.
/// 
/// **Validates: Requirements 2.3**
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepMissingParameterTests
{
    private CDKDeployStep CreateDeployStep(IConfiguration configuration)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });

        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);

        // Replace IConfiguration with our test configuration
        builder.Services.AddSingleton<IConfiguration>(configuration);

        var services = builder.Services.BuildServiceProvider();
        return services.GetRequiredService<CDKDeployStep>();
    }

    [Theory]
    [InlineData("Param-api-key", "api-key")]
    [InlineData("Param-db-password", "db-password")]
    [InlineData("Param-connection-string", "connection-string")]
    [InlineData("MyCustomParam", "MyCustomParam")]
    [InlineData("DatabaseUrl", "DatabaseUrl")]
    [InlineData("ServiceEndpoint", "ServiceEndpoint")]
    public void ResolveParameters_ThrowsForMissingRequiredParameter_WithCorrectMessage(
        string constructId, string expectedDerivedKey)
    {
        // Arrange
        var app = new App();
        var stack = new StackWithRequiredParameter(app, "TestStack", constructId);

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Empty configuration - no value for the parameter
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStep(configuration);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => deployStep.ResolveParameters(environment));

        // Verify the exception message contains the construct ID
        Assert.Contains(constructId, exception.Message);

        // Verify the exception message contains the expected configuration key
        var expectedConfigKey = $"Parameters:{expectedDerivedKey}";
        Assert.Contains(expectedConfigKey, exception.Message);
    }

    [Theory]
    [InlineData("Param-secret-key")]
    [InlineData("Param-token")]
    [InlineData("ApiSecret")]
    [InlineData("PrivateKey")]
    public void ResolveParameters_ThrowsForMissingRequiredParameter_RegardlessOfNoEcho(
        string constructId)
    {
        // Arrange
        var app = new App();
        var stack = new StackWithNoEchoRequiredParameter(app, "TestStack", constructId);

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Empty configuration - no value for the parameter
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStep(configuration);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => deployStep.ResolveParameters(environment));

        // Verify the exception message contains the construct ID
        Assert.Contains(constructId, exception.Message);
    }

    /// <summary>
    /// Test stack that creates a single CfnParameter with NO default value.
    /// </summary>
    private class StackWithRequiredParameter : Stack
    {
        public StackWithRequiredParameter(Construct scope, string id, string parameterConstructId)
            : base(scope, id, null)
        {
            new CfnParameter(this, parameterConstructId, new CfnParameterProps
            {
                Type = "String",
                Description = $"Test parameter '{parameterConstructId}'"
            });
        }
    }

    /// <summary>
    /// Test stack that creates a single CfnParameter with NoEcho=true and NO default value.
    /// </summary>
    private class StackWithNoEchoRequiredParameter : Stack
    {
        public StackWithNoEchoRequiredParameter(Construct scope, string id, string parameterConstructId)
            : base(scope, id, null)
        {
            new CfnParameter(this, parameterConstructId, new CfnParameterProps
            {
                Type = "String",
                Description = $"Secret test parameter '{parameterConstructId}'",
                NoEcho = true
            });
        }
    }
}
