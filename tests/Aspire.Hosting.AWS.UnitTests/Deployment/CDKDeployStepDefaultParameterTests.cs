// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 4: Parameters with defaults are skipped when unconfigured

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Constructs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Property 4: Parameters with defaults are skipped when unconfigured
/// 
/// For any CfnParameter that has a default value defined, if no corresponding value exists
/// in IConfiguration at the derived key, the parameter SHALL NOT appear in the resolved
/// parameter list and no error SHALL be thrown.
/// 
/// **Validates: Requirements 2.4**
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepDefaultParameterTests
{
    private readonly ILogger<CDKDeployStep> _logger = NullLogger<CDKDeployStep>.Instance;

    [Theory]
    [InlineData("Param-api-key", "default-api-key")]
    [InlineData("Param-db-password", "default-password")]
    [InlineData("Param-connection-string", "Server=localhost;Database=test")]
    [InlineData("MyCustomParam", "custom-default-value")]
    [InlineData("DatabaseUrl", "https://default-db.example.com")]
    [InlineData("ServiceEndpoint", "http://localhost:8080")]
    public void ResolveParameters_SkipsParameterWithDefault_WhenNoConfigEntry(
        string constructId, string defaultValue)
    {
        // Arrange
        var app = new App();
        var stack = new StackWithDefaultParameter(app, "TestStack", constructId, defaultValue);

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

        var deployStep = new CDKDeployStep(
            null!,
            _logger,
            null!,
            configuration);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - parameter with default should be skipped (not in results)
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("Param-optional-setting", "fallback-value")]
    [InlineData("OptionalFeatureFlag", "true")]
    [InlineData("Param-timeout-seconds", "30")]
    public void ResolveParameters_DoesNotThrow_WhenParameterHasDefault(
        string constructId, string defaultValue)
    {
        // Arrange
        var app = new App();
        var stack = new StackWithDefaultParameter(app, "TestStack", constructId, defaultValue);

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

        var deployStep = new CDKDeployStep(
            null!,
            _logger,
            null!,
            configuration);

        // Act & Assert - should not throw any exception
        var exception = Record.Exception(() => deployStep.ResolveParameters(environment));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Param-configured-param", "Param-default-param")]
    [InlineData("RequiredParam", "OptionalParam")]
    public void ResolveParameters_ReturnsOnlyConfiguredParameters_SkipsDefaulted(
        string configuredParamId, string defaultedParamId)
    {
        // Arrange
        var app = new App();
        var stack = new Stack(app, "TestStack");

        // Create a parameter WITHOUT a default (requires config)
        new CfnParameter(stack, configuredParamId, new CfnParameterProps
        {
            Type = "String",
            Description = $"Required parameter '{configuredParamId}'"
        });

        // Create a parameter WITH a default (should be skipped)
        new CfnParameter(stack, defaultedParamId, new CfnParameterProps
        {
            Type = "String",
            Default = "some-default-value",
            Description = $"Optional parameter '{defaultedParamId}'"
        });

        // Provide config only for the required parameter
        var derivedKey = configuredParamId.StartsWith("Param-", StringComparison.Ordinal)
            ? configuredParamId.Substring("Param-".Length)
            : configuredParamId;

        var configValues = new Dictionary<string, string?>
        {
            [$"Parameters:{derivedKey}"] = "configured-value"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);
        environment.InitializeCDKApp(null, Path.GetTempPath());

        var deployStep = new CDKDeployStep(
            null!,
            _logger,
            null!,
            configuration);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - only the configured parameter should be in results
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(configuredParamId), result[0].TemplateParameterName);
        Assert.Equal("configured-value", result[0].Value);

        // The defaulted parameter should NOT be in results
        Assert.DoesNotContain(result, p => p.TemplateParameterName == CDKDeployStep.ConvertToTemplateParameterName(defaultedParamId));
    }

    /// <summary>
    /// Test stack that creates a single CfnParameter WITH a default value.
    /// </summary>
    private class StackWithDefaultParameter : Stack
    {
        public StackWithDefaultParameter(Construct scope, string id, string parameterConstructId, string defaultValue)
            : base(scope, id, null)
        {
            new CfnParameter(this, parameterConstructId, new CfnParameterProps
            {
                Type = "String",
                Default = defaultValue,
                Description = $"Test parameter '{parameterConstructId}' with default"
            });
        }
    }
}
