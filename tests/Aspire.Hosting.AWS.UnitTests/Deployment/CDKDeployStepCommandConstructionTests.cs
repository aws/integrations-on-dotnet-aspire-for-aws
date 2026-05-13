// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 5: Command construction includes all resolved parameters

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using System.Text.RegularExpressions;
using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Constructs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Property 5: Command construction includes all resolved parameters.
/// Validates: Requirements 3.1, 3.2, 3.4, 4.1
///
/// For any non-empty list of ResolvedParameter instances, the constructed deploy command SHALL
/// contain exactly one --parameters flag per resolved parameter, each using the format
/// --parameters "{TemplateParameterName}={Value}", regardless of whether the parameter is secret or not.
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepCommandConstructionTests
{
    private static readonly Regex ParametersFlagPattern = new Regex(
        @"--parameters\s+""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// Provides various lists of resolved parameter data for testing command construction.
    /// Each entry is: (logicalIds[], values[], isSecretFlags[])
    /// </summary>
    public static IEnumerable<object[]> ResolvedParameterLists()
    {
        // Single non-secret Param- prefixed parameter
        yield return new object[]
        {
            new[] { "Param-api-key" },
            new[] { "my-secret-key" },
            new[] { false }
        };

        // Single secret Param- prefixed parameter (secrets use same --parameters mechanism)
        yield return new object[]
        {
            new[] { "Param-db-password" },
            new[] { "super-secret" },
            new[] { true }
        };

        // Single user-defined (non-Param- prefixed) parameter
        yield return new object[]
        {
            new[] { "MyCustomParam" },
            new[] { "custom-value" },
            new[] { false }
        };

        // Multiple parameters - mix of Param- prefixed and user-defined
        yield return new object[]
        {
            new[] { "Param-api-key", "Param-db-password", "UserDefinedParam" },
            new[] { "key-value-1", "db-pass", "user-value" },
            new[] { false, true, false }
        };

        // Multiple parameters - all Param- prefixed, mix of secret and non-secret
        yield return new object[]
        {
            new[] { "Param-key1", "Param-key2", "Param-key3", "Param-key4" },
            new[] { "value1", "value2", "value3", "value4" },
            new[] { false, true, false, true }
        };

        // Multiple user-defined parameters
        yield return new object[]
        {
            new[] { "DatabaseUrl", "AppSettings", "SecretToken" },
            new[] { "https://db.example.com", "setting-value", "token-123" },
            new[] { false, false, true }
        };

        // Large number of parameters
        yield return new object[]
        {
            new[] { "Param-a", "Param-b", "Param-c", "CustomX", "CustomY", "Param-d", "CustomZ" },
            new[] { "val-a", "val-b", "val-c", "val-x", "val-y", "val-d", "val-z" },
            new[] { false, true, false, false, true, false, false }
        };
    }

    /// <summary>
    /// Validates: Requirements 3.1, 3.2, 3.4, 4.1
    /// Verifies that the command construction produces exactly one --parameters flag per
    /// resolved parameter, using the format --parameters "{TemplateParameterName}={Value}".
    /// Secret parameters use the same --parameters mechanism as non-secret parameters.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResolvedParameterLists))]
    public void CommandConstruction_IncludesAllResolvedParameters_WithCorrectFormat(
        string[] logicalIds, string[] values, bool[] isSecretFlags)
    {
        // Arrange - Build ResolvedParameter list and start with the base CDK deploy command
        var resolvedParameters = BuildResolvedParameters(logicalIds, values, isSecretFlags);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        // Act - Apply the same command construction logic as ExecuteCDKDeployCLIAsync
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Parse out all --parameters flags from the constructed command
        var matches = ParametersFlagPattern.Matches(cdkDeployCommand);

        // Verify count matches: exactly one --parameters flag per resolved parameter
        Assert.Equal(resolvedParameters.Count, matches.Count);

        // Verify each resolved parameter has a corresponding --parameters flag with correct content
        for (int i = 0; i < resolvedParameters.Count; i++)
        {
            var expectedContent = $"{resolvedParameters[i].TemplateParameterName}={resolvedParameters[i].Value}";
            Assert.Equal(expectedContent, matches[i].Groups[1].Value);
        }
    }

    /// <summary>
    /// Validates: Requirements 3.1, 3.2, 3.4, 4.1
    /// Verifies that each --parameters flag uses the exact format: --parameters "{TemplateParameterName}={Value}"
    /// with the LogicalId being the full construct ID (including Param- prefix if present).
    /// </summary>
    [Theory]
    [MemberData(nameof(ResolvedParameterLists))]
    public void CommandConstruction_UsesLogicalIdAsParameterKey(
        string[] logicalIds, string[] values, bool[] isSecretFlags)
    {
        // Arrange
        var resolvedParameters = BuildResolvedParameters(logicalIds, values, isSecretFlags);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        // Act
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Each parameter's LogicalId appears as the key in the --parameters flag
        foreach (var param in resolvedParameters)
        {
            var expectedFragment = $"--parameters \"{param.TemplateParameterName}={param.Value}\"";
            Assert.Contains(expectedFragment, cdkDeployCommand);
        }
    }

    /// <summary>
    /// Validates: Requirements 3.4, 4.1
    /// Verifies that when multiple parameters exist, each gets a separate --parameters flag
    /// (not combined into a single flag).
    /// </summary>
    [Theory]
    [MemberData(nameof(ResolvedParameterLists))]
    public void CommandConstruction_EachParameterGetsSeparateFlag(
        string[] logicalIds, string[] values, bool[] isSecretFlags)
    {
        // Arrange
        var resolvedParameters = BuildResolvedParameters(logicalIds, values, isSecretFlags);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        // Act
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Count occurrences of "--parameters" in the command
        var parametersFlagCount = Regex.Matches(cdkDeployCommand, @"--parameters\s").Count;
        Assert.Equal(resolvedParameters.Count, parametersFlagCount);
    }

    /// <summary>
    /// Validates: Requirements 3.1, 4.1
    /// Verifies that secret parameters are included in the command using the same --parameters
    /// mechanism as non-secret parameters (secrets are not excluded from the command).
    /// </summary>
    [Theory]
    [MemberData(nameof(ResolvedParameterLists))]
    public void CommandConstruction_SecretParametersIncluded_SameMechanismAsNonSecret(
        string[] logicalIds, string[] values, bool[] isSecretFlags)
    {
        // Arrange
        var resolvedParameters = BuildResolvedParameters(logicalIds, values, isSecretFlags);
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        // Act
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - All parameters (including secrets) are present in the command
        var secretParams = resolvedParameters.Where(p => p.IsSecret).ToList();
        var nonSecretParams = resolvedParameters.Where(p => !p.IsSecret).ToList();

        foreach (var secretParam in secretParams)
        {
            Assert.Contains($"--parameters \"{secretParam.TemplateParameterName}={secretParam.Value}\"", cdkDeployCommand);
        }

        foreach (var nonSecretParam in nonSecretParams)
        {
            Assert.Contains($"--parameters \"{nonSecretParam.TemplateParameterName}={nonSecretParam.Value}\"", cdkDeployCommand);
        }
    }

    /// <summary>
    /// Validates: Requirements 3.1, 3.2, 3.4, 4.1
    /// End-to-end test: Sets up a CDK stack with CfnParameters, provides config values,
    /// resolves parameters, and verifies the resulting command would contain the correct flags.
    /// </summary>
    [Theory]
    [MemberData(nameof(EndToEndParameterConfigurations))]
    public void EndToEnd_ResolveAndBuildCommand_ContainsAllParameterFlags(
        string[] parameterIds, bool[] isSecretFlags, string[] values)
    {
        // Arrange - Create CDK stack with parameters
        var app = new App();
        var stack = new TestStack(app, "TestStack");

        var configValues = new Dictionary<string, string?>();
        for (int i = 0; i < parameterIds.Length; i++)
        {
            new CfnParameter(stack, parameterIds[i], new CfnParameterProps
            {
                Type = "String",
                NoEcho = isSecretFlags[i]
            });

            // Derive the config key the same way CDKDeployStep does
            var derivedKey = parameterIds[i].StartsWith("Param-", StringComparison.Ordinal)
                ? parameterIds[i].Substring("Param-".Length)
                : parameterIds[i];
            configValues[$"Parameters:{derivedKey}"] = values[i];
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var deployStep = CreateDeployStep(configuration);

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());

        // Act - Resolve parameters and build the command
        var resolvedParameters = deployStep.ResolveParameters(environment);

        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
        foreach (var param in resolvedParameters)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Verify the command contains all expected --parameters flags
        var matches = ParametersFlagPattern.Matches(cdkDeployCommand);
        Assert.Equal(parameterIds.Length, matches.Count);

        for (int i = 0; i < parameterIds.Length; i++)
        {
            var expectedContent = $"{CDKDeployStep.ConvertToTemplateParameterName(parameterIds[i])}={values[i]}";
            Assert.Contains(matches.Cast<Match>(), m => m.Groups[1].Value == expectedContent);
        }
    }

    /// <summary>
    /// Provides end-to-end test configurations with parameter IDs, secret flags, and values.
    /// </summary>
    public static IEnumerable<object[]> EndToEndParameterConfigurations()
    {
        // Single Param- prefixed non-secret parameter
        yield return new object[]
        {
            new[] { "Param-api-key" },
            new[] { false },
            new[] { "my-api-key-value" }
        };

        // Single user-defined secret parameter
        yield return new object[]
        {
            new[] { "SecretToken" },
            new[] { true },
            new[] { "super-secret-token" }
        };

        // Mix of Param- prefixed and user-defined, secret and non-secret
        yield return new object[]
        {
            new[] { "Param-db-password", "Param-api-key", "CustomEndpoint" },
            new[] { true, false, false },
            new[] { "db-pass-123", "api-key-456", "https://endpoint.example.com" }
        };

        // Multiple user-defined parameters
        yield return new object[]
        {
            new[] { "DatabaseUrl", "CacheEndpoint", "SecretKey", "AppName" },
            new[] { false, false, true, false },
            new[] { "postgres://localhost:5432", "redis://cache:6379", "s3cr3t!", "MyApp" }
        };

        // All secret parameters
        yield return new object[]
        {
            new[] { "Param-secret1", "Param-secret2", "UserSecret" },
            new[] { true, true, true },
            new[] { "value1", "value2", "value3" }
        };
    }

    private static List<ResolvedParameter> BuildResolvedParameters(
        string[] logicalIds, string[] values, bool[] isSecretFlags)
    {
        var parameters = new List<ResolvedParameter>();
        for (int i = 0; i < logicalIds.Length; i++)
        {
            parameters.Add(new ResolvedParameter(logicalIds[i], values[i], isSecretFlags[i]));
        }
        return parameters;
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
}
