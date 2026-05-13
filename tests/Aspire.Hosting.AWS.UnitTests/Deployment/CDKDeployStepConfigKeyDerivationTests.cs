// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 2: Configuration key derivation is correct for both conventions

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
/// Property 2: Configuration key derivation is correct for both conventions.
/// Validates: Requirements 2.1, 2.2
///
/// For any CfnParameter with a Param- prefixed construct ID, the configuration lookup key
/// SHALL be Parameters:{name-with-prefix-stripped}.
/// For any CfnParameter with a non-Param- prefixed construct ID, the configuration lookup key
/// SHALL be Parameters:{full-construct-id}.
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepConfigKeyDerivationTests
{
    private CDKDeployStep CreateDeployStep(IConfiguration configuration)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });

        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);
        builder.Services.AddSingleton<IConfiguration>(configuration);

        var services = builder.Services.BuildServiceProvider();
        return services.GetRequiredService<CDKDeployStep>();
    }

    /// <summary>
    /// Validates: Requirements 2.1, 2.2
    /// For Param- prefixed construct IDs: config key is Parameters:{name-with-prefix-stripped}
    /// For non-Param- prefixed construct IDs: config key is Parameters:{full-construct-id}
    /// </summary>
    [Theory]
    [InlineData("Param-api-key", "Parameters:api-key", "secret-value-1")]
    [InlineData("Param-db-password", "Parameters:db-password", "my-db-pass")]
    [InlineData("Param-connection-string", "Parameters:connection-string", "Server=localhost")]
    [InlineData("MyCustomParam", "Parameters:MyCustomParam", "custom-value")]
    [InlineData("DatabaseUrl", "Parameters:DatabaseUrl", "https://db.example.com")]
    [InlineData("AppSettings", "Parameters:AppSettings", "setting-value")]
    public void ResolveParameters_DerivesCorrectConfigKey_ForBothConventions(
        string constructId, string expectedConfigKey, string configValue)
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { expectedConfigKey, configValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStep(configuration);

        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String"
        });

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - The parameter was resolved, proving the correct config key was used
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(constructId), result[0].TemplateParameterName);
        Assert.Equal(configValue, result[0].Value);
    }

    /// <summary>
    /// Validates: Requirements 2.1, 2.2
    /// Verifies that when the config key is set incorrectly (without stripping prefix for Param- IDs),
    /// the parameter is NOT resolved (proving the correct key derivation is required).
    /// </summary>
    [Theory]
    [InlineData("Param-api-key", "Parameters:Param-api-key", "wrong-key-value")]
    [InlineData("Param-db-password", "Parameters:Param-db-password", "wrong-key-value")]
    public void ResolveParameters_DoesNotResolve_WhenConfigKeyIncludesPrefix(
        string constructId, string wrongConfigKey, string configValue)
    {
        // Arrange - Set config with the WRONG key (not stripping Param- prefix)
        // Also provide a default so it doesn't throw
        var configData = new Dictionary<string, string?>
        {
            { wrongConfigKey, configValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStep(configuration);

        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String",
            Default = "fallback-default"
        });

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - Parameter is NOT resolved because the wrong config key was used,
        // and it has a default so it's skipped rather than throwing
        Assert.Empty(result);
    }

    /// <summary>
    /// Validates: Requirements 2.1, 2.2
    /// Verifies that a stack with mixed parameter conventions resolves each using the correct key.
    /// </summary>
    [Fact]
    public void ResolveParameters_MixedConventions_ResolvesEachWithCorrectKey()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Parameters:api-key", "secret-api-key" },       // For Param-api-key
            { "Parameters:MyCustomParam", "custom-value" }    // For MyCustomParam
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStep(configuration);

        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, "Param-api-key", new CfnParameterProps { Type = "String" });
        new CfnParameter(stack, "MyCustomParam", new CfnParameterProps { Type = "String" });

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.TemplateParameterName == CDKDeployStep.ConvertToTemplateParameterName("Param-api-key") && p.Value == "secret-api-key");
        Assert.Contains(result, p => p.TemplateParameterName == CDKDeployStep.ConvertToTemplateParameterName("MyCustomParam") && p.Value == "custom-value");
    }

    private class TestStack : Stack
    {
        public TestStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }
}
