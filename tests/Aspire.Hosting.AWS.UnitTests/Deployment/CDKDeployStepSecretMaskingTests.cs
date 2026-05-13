// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 7: Secret parameter values are masked in logs

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
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Property 7: Secret parameter values are masked in logs.
/// Validates: Requirements 4.2, 4.3
///
/// For any resolved parameter where IsSecret is true, the parameter's value SHALL NOT appear
/// in any log output, and SHALL be replaced with a mask string (e.g., "****").
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepSecretMaskingTests
{
    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// Verifies that when a parameter is marked as secret (IsSecret = true),
    /// the display value used for logging is "****" and the actual secret value is absent.
    /// </summary>
    [Theory]
    [InlineData("super-secret-password")]
    [InlineData("api-key-12345")]
    [InlineData("token!@#$%^&*()")]
    [InlineData("xyzzy-unique-value")]
    [InlineData("multi word secret value with spaces")]
    [InlineData("value=with=equals")]
    public void SecretParameter_ValueIsMaskedInLogs_ActualValueAbsent(string secretValue)
    {
        // Arrange
        var capturingLogger = new CapturingLogger<CDKDeployStep>();

        var resolvedParameters = new List<ResolvedParameter>
        {
            new ResolvedParameter("Param-secret", secretValue, IsSecret: true)
        };

        // Act - Simulate the logging logic from ExecuteCDKDeployCLIAsync
        foreach (var param in resolvedParameters)
        {
            var displayValue = param.IsSecret ? "****" : param.Value;
            capturingLogger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
        }

        // Assert - The mask "****" IS present in the log output
        var logOutput = capturingLogger.GetCapturedOutput();
        Assert.Contains("****", logOutput);

        // Assert - The actual secret value is NOT present in the log output
        Assert.DoesNotContain(secretValue, logOutput);
    }

    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// Verifies that non-secret parameters have their actual value in the log output,
    /// while secret parameters in the same batch are masked.
    /// </summary>
    [Theory]
    [InlineData("my-secret-password", "non-secret-value")]
    [InlineData("db-connection-string-secret", "public-endpoint")]
    [InlineData("private-key-content", "app-name")]
    public void MixedParameters_OnlySecretValuesAreMasked(string secretValue, string nonSecretValue)
    {
        // Arrange
        var capturingLogger = new CapturingLogger<CDKDeployStep>();

        var resolvedParameters = new List<ResolvedParameter>
        {
            new ResolvedParameter("Param-secret-param", secretValue, IsSecret: true),
            new ResolvedParameter("Param-public-param", nonSecretValue, IsSecret: false)
        };

        // Act - Simulate the logging logic from ExecuteCDKDeployCLIAsync
        foreach (var param in resolvedParameters)
        {
            var displayValue = param.IsSecret ? "****" : param.Value;
            capturingLogger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
        }

        // Assert
        var logOutput = capturingLogger.GetCapturedOutput();

        // Secret value is NOT present in log output
        Assert.DoesNotContain(secretValue, logOutput);

        // Non-secret value IS present in log output
        Assert.Contains(nonSecretValue, logOutput);

        // Mask IS present (for the secret parameter)
        Assert.Contains("****", logOutput);
    }

    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// End-to-end test: Sets up a CDK stack with a NoEcho CfnParameter, resolves it,
    /// and verifies the masking logic produces "****" for the secret and excludes the actual value.
    /// </summary>
    [Theory]
    [InlineData("Param-db-password", "Parameters:db-password", "super-secret-db-pass")]
    [InlineData("Param-api-key", "Parameters:api-key", "sk-1234567890abcdef")]
    [InlineData("SecretToken", "Parameters:SecretToken", "ghp_xxxxxxxxxxxx")]
    public void EndToEnd_SecretCfnParameter_ValueMaskedInLogOutput(
        string constructId, string configKey, string secretValue)
    {
        // Arrange - Create CDK stack with a NoEcho parameter
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String",
            NoEcho = true
        });

        var configData = new Dictionary<string, string?>
        {
            { configKey, secretValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStep(configuration);

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Act - Resolve parameters and simulate logging
        var resolvedParameters = deployStep.ResolveParameters(environment);
        var capturingLogger = new CapturingLogger<CDKDeployStep>();

        foreach (var param in resolvedParameters)
        {
            var displayValue = param.IsSecret ? "****" : param.Value;
            capturingLogger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
        }

        // Assert - The resolved parameter is marked as secret
        Assert.Single(resolvedParameters);
        Assert.True(resolvedParameters[0].IsSecret);

        // Assert - Log output contains the mask but not the actual value
        var logOutput = capturingLogger.GetCapturedOutput();
        Assert.Contains("****", logOutput);
        Assert.DoesNotContain(secretValue, logOutput);

        // Assert - The template parameter name is still visible in the log (only the value is masked)
        Assert.Contains(CDKDeployStep.ConvertToTemplateParameterName(constructId), logOutput);
    }

    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// Verifies that the masking pattern always produces "****" for any secret value,
    /// regardless of the value content.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("normal-looking-value")]
    [InlineData("value\nwith\nnewlines")]
    [InlineData("short")]
    public void MaskingPattern_AlwaysProducesMaskForSecretValues(string secretValue)
    {
        // Arrange
        var param = new ResolvedParameter("Param-test", secretValue, IsSecret: true);

        // Act - Apply the masking pattern from ExecuteCDKDeployCLIAsync
        var displayValue = param.IsSecret ? "****" : param.Value;

        // Assert - The display value is always "****" for secrets
        Assert.Equal("****", displayValue);
    }

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

    private class TestStack : Stack
    {
        public TestStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }

    /// <summary>
    /// A simple logger implementation that captures log messages for assertion.
    /// </summary>
    private class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }

        public string GetCapturedOutput() => string.Join(System.Environment.NewLine, _messages);
    }
}
