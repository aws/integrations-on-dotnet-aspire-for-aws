// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

/// <summary>
/// Tests for the OpenTelemetry opt-out on <c>AddAWSLambdaFunction</c>. By default the Lambda resource is wired to
/// export telemetry via OTLP; <see cref="LambdaFunctionOptions.DisableOpenTelemetry"/> skips that wiring for
/// scenarios where no collector or dashboard is listening (for example integration tests).
/// </summary>
public class LambdaOpenTelemetryTests
{
    private sealed class FakeLambdaProject : IProjectMetadata
    {
        public string ProjectPath => "fakeLambda.csproj";
    }

    [Fact]
    public void AddAWSLambdaFunction_WiresOtlpExporter_ByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        var lambda = builder.AddAWSLambdaFunction<FakeLambdaProject>("MyFunction", "MyFunction::MyFunction.Function::Handler");
        Assert.Single(lambda.Resource.Annotations.OfType<OtlpExporterAnnotation>());
    }

    [Fact]
    public void AddAWSLambdaFunction_SkipsOtlpExporter_WhenDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        var lambda = builder.AddAWSLambdaFunction<FakeLambdaProject>(
            "MyFunction",
            "MyFunction::MyFunction.Function::Handler",
            new LambdaFunctionOptions { DisableOpenTelemetry = true }
        );

        Assert.Empty(lambda.Resource.Annotations.OfType<OtlpExporterAnnotation>());
    }
}
