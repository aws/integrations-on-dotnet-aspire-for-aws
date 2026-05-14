// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

// Feature: deployment-parameters, Property 1: Parameter discovery finds all CfnParameters

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES001

using Amazon.CDK;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

/// <summary>
/// Property 1: Parameter discovery finds all CfnParameters
/// For any CDK stack containing an arbitrary mix of CfnParameter constructs (with and without the Param- prefix)
/// and non-CfnParameter constructs, the discovery method SHALL return exactly those constructs that are
/// CfnParameter instances, regardless of their construct ID.
/// 
/// **Validates: Requirements 1.1, 1.3**
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepParameterDiscoveryTests
{
    /// <summary>
    /// Provides test data with different construct configurations.
    /// Each entry is: (parameterIds, nonParameterConstructIds)
    /// where parameterIds are the CfnParameter construct IDs to create,
    /// and nonParameterConstructIds are non-CfnParameter constructs to add.
    /// All CfnParameters have config values provided so the method doesn't throw.
    /// </summary>
    public static IEnumerable<object[]> ConstructConfigurations()
    {
        // Single Param- prefixed parameter, no other constructs
        yield return new object[]
        {
            new[] { "Param-api-key" },
            Array.Empty<string>()
        };

        // Single user-defined parameter (no Param- prefix), no other constructs
        yield return new object[]
        {
            new[] { "MyCustomParam" },
            Array.Empty<string>()
        };

        // Multiple Param- prefixed parameters with non-CfnParameter constructs
        yield return new object[]
        {
            new[] { "Param-db-password", "Param-api-key", "Param-connection-string" },
            new[] { "MyBucket", "MyQueue" }
        };

        // Mix of Param- prefixed and user-defined parameters with non-CfnParameter constructs
        yield return new object[]
        {
            new[] { "Param-secret", "UserDefinedParam", "Param-another", "CustomId" },
            new[] { "SomeBucket", "SomeTable", "SomeQueue" }
        };

        // Only non-CfnParameter constructs (no parameters)
        yield return new object[]
        {
            Array.Empty<string>(),
            new[] { "Bucket1", "Queue1", "Table1" }
        };

        // Many parameters with various naming conventions
        yield return new object[]
        {
            new[] { "Param-a", "Param-b", "X", "Y", "Param-z" },
            new[] { "Resource1" }
        };
    }

    [Theory]
    [MemberData(nameof(ConstructConfigurations))]
    public void ResolveParameters_FindsAllCfnParameters_RegardlessOfConstructId(
        string[] parameterIds, string[] nonParameterConstructIds)
    {
        // Arrange
        var app = new App();
        var stack = new Stack(app, "TestStack");

        // Create CfnParameter constructs with config values provided
        var configValues = new Dictionary<string, string?>();
        foreach (var paramId in parameterIds)
        {
            new CfnParameter(stack, paramId, new CfnParameterProps
            {
                Type = "String",
                Description = $"Test parameter {paramId}"
            });

            // Derive the config key the same way CDKDeployStep does
            var derivedKey = paramId.StartsWith("Param-", StringComparison.Ordinal)
                ? paramId.Substring("Param-".Length)
                : paramId;
            configValues[$"Parameters:{derivedKey}"] = $"value-for-{paramId}";
        }

        // Create non-CfnParameter constructs (CfnOutput as a representative non-parameter construct)
        foreach (var constructId in nonParameterConstructIds)
        {
            new CfnOutput(stack, constructId, new CfnOutputProps
            {
                Value = "dummy-value"
            });
        }

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
            new StubProcessCommandService(),
            NullLogger<CDKDeployStep>.Instance,
            new StubAWSEnvironmentService(),
            configuration);

        // Act
        var resolvedParameters = deployStep.ResolveParameters(environment);

        // Assert - discovery returns exactly the CfnParameter instances count
        Assert.Equal(parameterIds.Length, resolvedParameters.Count);

        // Verify each CfnParameter's construct ID is present in the resolved list
        foreach (var paramId in parameterIds)
        {
            Assert.Contains(resolvedParameters, p => p.TemplateParameterName == CDKDeployStep.ConvertToTemplateParameterName(paramId));
        }
    }

    /// <summary>
    /// Stub implementation of IAWSEnvironmentService for testing.
    /// </summary>
    private class StubAWSEnvironmentService : IAWSEnvironmentService
    {
        public string[] GetCommandLineArgs() => Array.Empty<string>();
    }

    /// <summary>
    /// Stub implementation of IProcessCommandService for testing.
    /// </summary>
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
}
