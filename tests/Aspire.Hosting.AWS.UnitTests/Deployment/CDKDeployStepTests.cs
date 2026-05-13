// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using System.Text.RegularExpressions;
using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Utils.Internal;
using Constructs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Comprehensive unit tests for CDKDeployStep parameter resolution.
/// Covers DI resolution, parameter discovery, config key derivation, command construction,
/// missing parameter errors, default parameter skipping, and secret masking.
///
/// Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3, 3.1, 4.2, 4.3, 5.1, 5.3
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepTests
{
    private static readonly Regex ParametersFlagPattern = new Regex(
        @"--parameters\s+""([^""]+)""",
        RegexOptions.Compiled);

    #region DI Resolution Tests

    /// <summary>
    /// Validates: Requirement 5.1
    /// CDKDeployStep resolves from service collection with IConfiguration injected.
    /// </summary>
    [Fact]
    public void CDKDeployStep_ResolvesFromServiceCollection_WithIConfigurationInjected()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });

        builder.AddAWSCDKEnvironment("TestEnv", CDKDefaultsProviderFactory.Preview_V1);
        builder.Services.AddSingleton<IConfiguration>(configuration);

        var services = builder.Services.BuildServiceProvider();

        // Act
        var deployStep = services.GetRequiredService<CDKDeployStep>();

        // Assert
        Assert.NotNull(deployStep);
    }

    #endregion

    #region No Parameters Tests

    /// <summary>
    /// Validates: Requirement 1.2, 5.3
    /// Stack with no CfnParameter children produces command without --parameters flags.
    /// </summary>
    [Fact]
    public void ResolveParameters_NoCfnParameters_ReturnsEmptyList()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        // Add non-parameter constructs only
        new CfnOutput(stack, "OutputA", new CfnOutputProps { Value = "value-a" });
        new CfnOutput(stack, "OutputB", new CfnOutputProps { Value = "value-b" });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);

        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Validates: Requirement 5.3
    /// When no parameters are resolved, the deploy command remains unchanged (backward compatible).
    /// </summary>
    [Fact]
    public void CommandConstruction_NoParameters_NoParametersFlagsAppended()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert
        Assert.DoesNotContain("--parameters", cdkDeployCommand);
        Assert.Equal("cdk deploy --no-notices --require-approval never --app .", cdkDeployCommand);
    }

    #endregion

    #region Param- Prefix Config Key Tests

    /// <summary>
    /// Validates: Requirement 2.1
    /// Stack with Param- prefixed parameters resolves config key with prefix stripped.
    /// </summary>
    [Theory]
    [InlineData("Param-api-key", "api-key", "my-api-key")]
    [InlineData("Param-db-password", "db-password", "secret-pass")]
    [InlineData("Param-connection-string", "connection-string", "Server=localhost")]
    [InlineData("Param-a", "a", "short-key")]
    [InlineData("Param-multi-dash-name", "multi-dash-name", "value-123")]
    public void ResolveParameters_ParamPrefixed_ResolvesConfigKeyWithPrefixStripped(
        string constructId, string expectedStrippedKey, string configValue)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps { Type = "String" });

        var configData = new Dictionary<string, string?>
        {
            { $"Parameters:{expectedStrippedKey}", configValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(constructId), result[0].TemplateParameterName);
        Assert.Equal(configValue, result[0].Value);
    }

    #endregion

    #region User-Defined (Non-Param- Prefixed) Config Key Tests

    /// <summary>
    /// Validates: Requirement 2.2
    /// Stack with user-defined (non-Param- prefixed) parameters resolves config key using full construct ID.
    /// </summary>
    [Theory]
    [InlineData("MyCustomParam", "custom-value")]
    [InlineData("DatabaseUrl", "https://db.example.com")]
    [InlineData("ServiceEndpoint", "http://localhost:8080")]
    [InlineData("CacheTimeout", "300")]
    [InlineData("FeatureFlag", "true")]
    public void ResolveParameters_UserDefined_ResolvesConfigKeyUsingFullConstructId(
        string constructId, string configValue)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps { Type = "String" });

        var configData = new Dictionary<string, string?>
        {
            { $"Parameters:{constructId}", configValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(constructId), result[0].TemplateParameterName);
        Assert.Equal(configValue, result[0].Value);
    }

    #endregion

    #region Mixed Parameters Command Construction Tests

    /// <summary>
    /// Validates: Requirements 3.1, 1.1, 1.2
    /// Stack with mixed parameters (both conventions) produces correct --parameters flags in command.
    /// </summary>
    public static IEnumerable<object[]> MixedParameterConfigurations()
    {
        // Mix of Param- prefixed and user-defined
        yield return new object[]
        {
            new[] { "Param-api-key", "MyCustomParam" },
            new[] { "Parameters:api-key", "Parameters:MyCustomParam" },
            new[] { "secret-key-123", "custom-value" }
        };

        // Multiple Param- prefixed with one user-defined
        yield return new object[]
        {
            new[] { "Param-db-password", "Param-api-key", "ServiceEndpoint" },
            new[] { "Parameters:db-password", "Parameters:api-key", "Parameters:ServiceEndpoint" },
            new[] { "db-pass", "api-key-val", "https://svc.example.com" }
        };

        // All user-defined
        yield return new object[]
        {
            new[] { "DatabaseUrl", "CacheEndpoint", "AppName" },
            new[] { "Parameters:DatabaseUrl", "Parameters:CacheEndpoint", "Parameters:AppName" },
            new[] { "postgres://localhost", "redis://cache:6379", "MyApp" }
        };

        // Single parameter
        yield return new object[]
        {
            new[] { "Param-single" },
            new[] { "Parameters:single" },
            new[] { "only-value" }
        };
    }

    [Theory]
    [MemberData(nameof(MixedParameterConfigurations))]
    public void ResolveParameters_MixedConventions_ProducesCorrectParameterFlags(
        string[] constructIds, string[] configKeys, string[] values)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        var configData = new Dictionary<string, string?>();
        for (int i = 0; i < constructIds.Length; i++)
        {
            new CfnParameter(stack, constructIds[i], new CfnParameterProps { Type = "String" });
            configData[configKeys[i]] = values[i];
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Correct number of --parameters flags
        var matches = ParametersFlagPattern.Matches(cdkDeployCommand);
        Assert.Equal(constructIds.Length, matches.Count);

        // Assert - Each parameter is present with correct format
        for (int i = 0; i < constructIds.Length; i++)
        {
            var expectedName = CDKDeployStep.ConvertToTemplateParameterName(constructIds[i]);
            var expectedFragment = $"--parameters \"{expectedName}={values[i]}\"";
            Assert.Contains(expectedFragment, cdkDeployCommand);
        }
    }

    #endregion

    #region Missing Required Parameter Tests

    /// <summary>
    /// Validates: Requirement 2.3
    /// Missing required parameter (no default) throws InvalidOperationException with descriptive message
    /// containing parameter name and config key.
    /// </summary>
    [Theory]
    [InlineData("Param-api-key", "api-key")]
    [InlineData("Param-db-password", "db-password")]
    [InlineData("MyCustomParam", "MyCustomParam")]
    [InlineData("DatabaseUrl", "DatabaseUrl")]
    [InlineData("Param-connection-string", "connection-string")]
    public void ResolveParameters_MissingRequiredParameter_ThrowsWithDescriptiveMessage(
        string constructId, string expectedDerivedKey)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String"
            // No Default property set - this is a required parameter
        });

        // Empty configuration - no value for the parameter
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => deployStep.ResolveParameters(environment));

        // Verify the exception message contains the construct ID (parameter name)
        Assert.Contains(constructId, exception.Message);

        // Verify the exception message contains the expected configuration key
        var expectedConfigKey = $"Parameters:{expectedDerivedKey}";
        Assert.Contains(expectedConfigKey, exception.Message);
    }

    #endregion

    #region Default Parameter Skipping Tests

    /// <summary>
    /// Validates: Requirement 2.4 (implied by 5.3 backward compatibility)
    /// Parameter with default value is skipped when not in configuration.
    /// </summary>
    [Theory]
    [InlineData("Param-optional-setting", "fallback-value")]
    [InlineData("OptionalFeatureFlag", "true")]
    [InlineData("Param-timeout", "30")]
    [InlineData("DefaultEndpoint", "http://localhost:3000")]
    public void ResolveParameters_ParameterWithDefault_SkippedWhenNotConfigured(
        string constructId, string defaultValue)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String",
            Default = defaultValue
        });

        // Empty configuration - no value for the parameter
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - parameter with default should be skipped
        Assert.Empty(result);
    }

    /// <summary>
    /// Validates: Requirement 2.4
    /// Parameter with default value is included when explicitly configured.
    /// </summary>
    [Theory]
    [InlineData("Param-optional", "Parameters:optional", "override-value", "default-value")]
    [InlineData("OptionalParam", "Parameters:OptionalParam", "explicit-value", "fallback")]
    public void ResolveParameters_ParameterWithDefault_IncludedWhenExplicitlyConfigured(
        string constructId, string configKey, string configValue, string defaultValue)
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, constructId, new CfnParameterProps
        {
            Type = "String",
            Default = defaultValue
        });

        var configData = new Dictionary<string, string?>
        {
            { configKey, configValue }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert - parameter is included because config value was provided
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(constructId), result[0].TemplateParameterName);
        Assert.Equal(configValue, result[0].Value);
    }

    #endregion

    #region Secret Masking Tests

    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// Secret parameter values are masked in log output.
    /// </summary>
    [Theory]
    [InlineData("Param-secret-key", "Parameters:secret-key", "super-secret-password-123")]
    [InlineData("Param-api-token", "Parameters:api-token", "sk-abcdef1234567890")]
    [InlineData("SecretToken", "Parameters:SecretToken", "ghp_xxxxxxxxxxxx")]
    public void ResolveParameters_SecretParameter_ValueMaskedInLogOutput(
        string constructId, string configKey, string secretValue)
    {
        // Arrange
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

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);

        // Simulate the logging logic from ExecuteCDKDeployCLIAsync
        var capturingLogger = new CapturingLogger<CDKDeployStep>();
        foreach (var param in resolvedParameters)
        {
            var displayValue = param.IsSecret ? "****" : param.Value;
            capturingLogger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
        }

        // Assert - The parameter is marked as secret
        Assert.Single(resolvedParameters);
        Assert.True(resolvedParameters[0].IsSecret);

        // Assert - Log output contains the mask but not the actual value
        var logOutput = capturingLogger.GetCapturedOutput();
        Assert.Contains("****", logOutput);
        Assert.DoesNotContain(secretValue, logOutput);

        // Assert - The template parameter name is still visible in the log
        Assert.Contains(CDKDeployStep.ConvertToTemplateParameterName(constructId), logOutput);
    }

    /// <summary>
    /// Validates: Requirements 4.2, 4.3
    /// Non-secret parameters have their values visible in logs while secrets are masked.
    /// </summary>
    [Fact]
    public void ResolveParameters_MixedSecretAndNonSecret_OnlySecretsMasked()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        new CfnParameter(stack, "Param-public-key", new CfnParameterProps
        {
            Type = "String",
            NoEcho = false
        });
        new CfnParameter(stack, "Param-secret-key", new CfnParameterProps
        {
            Type = "String",
            NoEcho = true
        });

        var configData = new Dictionary<string, string?>
        {
            { "Parameters:public-key", "visible-value" },
            { "Parameters:secret-key", "hidden-secret" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);

        var capturingLogger = new CapturingLogger<CDKDeployStep>();
        foreach (var param in resolvedParameters)
        {
            var displayValue = param.IsSecret ? "****" : param.Value;
            capturingLogger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
        }

        // Assert
        var logOutput = capturingLogger.GetCapturedOutput();
        Assert.Contains("visible-value", logOutput);
        Assert.DoesNotContain("hidden-secret", logOutput);
        Assert.Contains("****", logOutput);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Validates: Requirements 1.1, 2.1, 2.2
    /// Edge case: parameter with construct ID that starts with "Param-" but has no suffix after the prefix.
    /// </summary>
    [Fact]
    public void ResolveParameters_ParamPrefixOnly_ResolvesWithEmptyDerivedKey()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, "Param-", new CfnParameterProps { Type = "String" });

        var configData = new Dictionary<string, string?>
        {
            { "Parameters:", "value-for-empty-key" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var result = deployStep.ResolveParameters(environment);

        // Assert
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName("Param-"), result[0].TemplateParameterName);
        Assert.Equal("value-for-empty-key", result[0].Value);
    }

    /// <summary>
    /// Validates: Requirements 3.1, 4.1
    /// Edge case: multiple parameters with same value produce separate --parameters flags.
    /// </summary>
    [Fact]
    public void CommandConstruction_MultipleParametersSameValue_EachGetsSeparateFlag()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");
        new CfnParameter(stack, "Param-key1", new CfnParameterProps { Type = "String" });
        new CfnParameter(stack, "Param-key2", new CfnParameterProps { Type = "String" });

        var configData = new Dictionary<string, string?>
        {
            { "Parameters:key1", "same-value" },
            { "Parameters:key2", "same-value" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Two separate --parameters flags even though values are the same
        var matches = ParametersFlagPattern.Matches(cdkDeployCommand);
        Assert.Equal(2, matches.Count);
    }

    /// <summary>
    /// Validates: Requirements 1.1, 2.1, 2.2, 3.1
    /// Integration test: full flow from stack creation through command construction with mixed parameters.
    /// </summary>
    [Fact]
    public void EndToEnd_MixedParametersWithDefaultsAndSecrets_ProducesCorrectCommand()
    {
        // Arrange
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        // Required Param- prefixed (non-secret)
        new CfnParameter(stack, "Param-api-endpoint", new CfnParameterProps { Type = "String" });
        // Required Param- prefixed (secret)
        new CfnParameter(stack, "Param-db-password", new CfnParameterProps { Type = "String", NoEcho = true });
        // User-defined required
        new CfnParameter(stack, "CustomRegion", new CfnParameterProps { Type = "String" });
        // Parameter with default (should be skipped)
        new CfnParameter(stack, "Param-optional", new CfnParameterProps { Type = "String", Default = "default-val" });

        var configData = new Dictionary<string, string?>
        {
            { "Parameters:api-endpoint", "https://api.example.com" },
            { "Parameters:db-password", "super-secret" },
            { "Parameters:CustomRegion", "us-west-2" }
            // No entry for Param-optional - should use default
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var deployStep = CreateDeployStepDirect(configuration);
        var environment = CreateEnvironment(stack);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - 3 parameters resolved (the one with default is skipped)
        Assert.Equal(3, resolvedParameters.Count);

        // Verify each expected parameter is in the command (names are converted: hyphens stripped)
        Assert.Contains($"--parameters \"Paramapiendpoint=https://api.example.com\"", cdkDeployCommand);
        Assert.Contains($"--parameters \"Paramdbpassword=super-secret\"", cdkDeployCommand);
        Assert.Contains($"--parameters \"CustomRegion=us-west-2\"", cdkDeployCommand);

        // Verify the optional parameter is NOT in the command
        Assert.DoesNotContain("Paramoptional", cdkDeployCommand);

        // Verify secret flag is set correctly
        var dbPasswordParam = resolvedParameters.First(p => p.TemplateParameterName == "Paramdbpassword");
        Assert.True(dbPasswordParam.IsSecret);

        var apiEndpointParam = resolvedParameters.First(p => p.TemplateParameterName == "Paramapiendpoint");
        Assert.False(apiEndpointParam.IsSecret);
    }

    #endregion

    #region Helper Methods

    private static CDKDeployStep CreateDeployStepDirect(IConfiguration configuration)
    {
        return new CDKDeployStep(
            new StubProcessCommandService(),
            NullLogger<CDKDeployStep>.Instance,
            new StubAWSEnvironmentService(),
            configuration);
    }

    private static AWSCDKEnvironmentResource<Stack> CreateEnvironment(Stack stack)
    {
        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());
        return environment;
    }

    private class TestStack : Stack
    {
        public TestStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }

    private class StubProcessCommandService : IProcessCommandService
    {
        public int RunProcess(ILogger logger, string path, string arguments, string workingDirectory, bool streamOutputToLogger = false, IDictionary<string, string>? environmentVariables = null)
        {
            return 0;
        }

        public IProcessCommandService.RunProcessAndCaptureStdOutResult RunCDKProcess(ILogger? logger, LogLevel logLevel, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null)
        {
            return new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, string.Empty);
        }

        public Task<IProcessCommandService.RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOutputAsync(ILogger logger, string path, string arguments, string? workingDirectory, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, string.Empty));
        }
    }

    private class StubAWSEnvironmentService : IAWSEnvironmentService
    {
        public string[] GetCommandLineArgs() => Array.Empty<string>();
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

    #endregion
}
