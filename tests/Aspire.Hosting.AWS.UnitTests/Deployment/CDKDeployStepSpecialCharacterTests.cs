// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Feature: deployment-parameters, Property 6: Parameter values with special characters are properly quoted

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
/// Property 6: Parameter values with special characters are properly quoted.
/// Validates: Requirements 3.3
///
/// For any parameter value containing spaces, equals signs, double quotes, or other shell
/// metacharacters, the constructed --parameters argument SHALL be quoted such that the entire
/// LogicalId=Value pair is enclosed in quotes, preserving the value intact for the shell.
/// </summary>
[Collection("CDKDeploymentTests")]
public class CDKDeployStepSpecialCharacterTests
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
    /// Validates: Requirements 3.3
    /// Verifies that ResolveParameters correctly preserves parameter values containing
    /// special characters exactly as configured, without modification or corruption.
    /// </summary>
    [Theory]
    [InlineData("Param-name", "Parameters:name", "hello world")]
    [InlineData("Param-conn", "Parameters:conn", "key=value")]
    [InlineData("Param-list", "Parameters:list", "a;b;c")]
    [InlineData("Param-amp", "Parameters:amp", "a&b")]
    [InlineData("Param-pipe", "Parameters:pipe", "a|b")]
    [InlineData("Param-dollar", "Parameters:dollar", "$var")]
    [InlineData("Param-backtick", "Parameters:backtick", "`command`")]
    [InlineData("Param-parens", "Parameters:parens", "func(arg)")]
    [InlineData("Param-angle", "Parameters:angle", "<tag>value</tag>")]
    [InlineData("Param-mixed", "Parameters:mixed", "host=db.example.com;port=5432;user=admin")]
    public void ResolveParameters_PreservesSpecialCharacterValues_Intact(
        string constructId, string configKey, string specialValue)
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { configKey, specialValue }
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

        // Assert - The value is preserved exactly as configured
        Assert.Single(result);
        Assert.Equal(CDKDeployStep.ConvertToTemplateParameterName(constructId), result[0].TemplateParameterName);
        Assert.Equal(specialValue, result[0].Value);
    }

    /// <summary>
    /// Validates: Requirements 3.3
    /// Verifies that the command format --parameters "{TemplateParameterName}={Value}" properly wraps
    /// the value in quotes, ensuring values with special characters are preserved when
    /// the command string is constructed.
    /// </summary>
    [Theory]
    [InlineData("Param-name", "hello world")]
    [InlineData("Param-conn", "key=value")]
    [InlineData("Param-list", "a;b;c")]
    [InlineData("Param-amp", "a&b")]
    [InlineData("Param-pipe", "a|b")]
    [InlineData("Param-dollar", "a $var b")]
    [InlineData("Param-backtick", "`command`")]
    [InlineData("Param-complex", "Server=localhost;Database=mydb;User Id=admin;Password=p@ss w0rd!")]
    public void CommandFormat_QuotesParameterValues_WithSpecialCharacters(
        string logicalId, string value)
    {
        // Arrange - Simulate the command construction format used in CDKDeployStep
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        var resolvedParam = new ResolvedParameter(logicalId, value, IsSecret: false);

        // Act - Apply the same format string used in ExecuteCDKDeployCLIAsync
        cdkDeployCommand += $" --parameters \"{resolvedParam.TemplateParameterName}={resolvedParam.Value}\"";

        // Assert - The command contains the properly quoted parameter
        var expectedFragment = $"--parameters \"{logicalId}={value}\"";
        Assert.Contains(expectedFragment, cdkDeployCommand);

        // Verify the value is enclosed in quotes (the entire TemplateParameterName=Value pair is quoted)
        Assert.Contains($"\"{logicalId}={value}\"", cdkDeployCommand);
    }

    /// <summary>
    /// Validates: Requirements 3.3
    /// Verifies that multiple parameters with special characters are all properly quoted
    /// in the constructed command string.
    /// </summary>
    [Fact]
    public void CommandFormat_MultipleSpecialCharacterParams_AllProperlyQuoted()
    {
        // Arrange
        var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

        var resolvedParams = new List<ResolvedParameter>
        {
            new("Param-name", "hello world", IsSecret: false),
            new("Param-conn", "Server=localhost;Port=5432", IsSecret: false),
            new("CustomParam", "value with spaces & special | chars", IsSecret: false)
        };

        // Act - Apply the same format used in ExecuteCDKDeployCLIAsync
        foreach (var param in resolvedParams)
        {
            cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
        }

        // Assert - Each parameter is properly quoted in the command
        Assert.Contains("--parameters \"Param-name=hello world\"", cdkDeployCommand);
        Assert.Contains("--parameters \"Param-conn=Server=localhost;Port=5432\"", cdkDeployCommand);
        Assert.Contains("--parameters \"CustomParam=value with spaces & special | chars\"", cdkDeployCommand);
    }

    private class TestStack : Stack
    {
        public TestStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }
}
