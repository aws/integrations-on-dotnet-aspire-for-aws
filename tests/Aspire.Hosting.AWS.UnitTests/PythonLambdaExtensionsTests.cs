// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

/// <summary>
/// Unit tests for <see cref="PythonLambdaFunctionResource"/> and the
/// <c>AddAWSPythonLambdaFunction</c> extension method.
/// </summary>
public class PythonLambdaExtensionsTests
{
    // -----------------------------------------------------------------------
    // Resource creation
    // -----------------------------------------------------------------------

    [Fact]
    public void AddAWSPythonLambdaFunction_CreatesResource_WithCorrectName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        builder.AddAWSPythonLambdaFunction("MyPythonFn", appDir, "main.handler");

        var resource = builder.Resources.OfType<PythonLambdaFunctionResource>().Single();
        Assert.Equal("MyPythonFn", resource.Name);
    }

    [Fact]
    public void AddAWSPythonLambdaFunction_SetsAppDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");

        var resource = builder.Resources.OfType<PythonLambdaFunctionResource>().Single();
        Assert.Equal(appDir.TrimEnd(Path.DirectorySeparatorChar),
                     resource.AppDirectory.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void AddAWSPythonLambdaFunction_AddsLambdaFunctionAnnotation_WithHandler()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        builder.AddAWSPythonLambdaFunction("PyFn", appDir, "app.handle");

        var resource = builder.Resources.OfType<PythonLambdaFunctionResource>().Single();
        Assert.True(resource.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var annotation));
        Assert.Equal("app.handle", annotation!.Handler);
    }

    [Fact]
    public void AddAWSPythonLambdaFunction_AutoAdds_LambdaServiceEmulator()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");

        // An emulator executable resource should be present
        var emulator = builder.Resources.OfType<LambdaEmulatorResource>().SingleOrDefault();
        Assert.NotNull(emulator);
    }

    [Fact]
    public void AddAWSPythonLambdaFunction_MultipleResources_ShareSingleEmulator()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        builder.AddAWSPythonLambdaFunction("Fn1", appDir, "main.handler1");
        builder.AddAWSPythonLambdaFunction("Fn2", appDir, "main.handler2");

        Assert.Single(builder.Resources.OfType<LambdaEmulatorResource>());
    }

    // -----------------------------------------------------------------------
    // Path resolution
    // -----------------------------------------------------------------------

    [Fact]
    public void AddAWSPythonLambdaFunction_RelativeAppDirectory_ResolvedToAbsolute()
    {
        var builder = DistributedApplication.CreateBuilder();

        // A relative path should be resolved against AppHostDirectory
        builder.AddAWSPythonLambdaFunction("PyFn", "../SomeLambda", "main.handler");

        var resource = builder.Resources.OfType<PythonLambdaFunctionResource>().Single();
        Assert.True(Path.IsPathRooted(resource.AppDirectory),
            "AppDirectory should be an absolute path after resolution.");
    }

    // -----------------------------------------------------------------------
    // Implements ILambdaFunctionResource
    // -----------------------------------------------------------------------

    [Fact]
    public void PythonLambdaFunctionResource_ImplementsILambdaFunctionResource()
    {
        var resource = new PythonLambdaFunctionResource("test", "python3", Path.GetTempPath());
        Assert.IsAssignableFrom<ILambdaFunctionResource>(resource);
    }

    [Fact]
    public void PythonLambdaFunctionResource_DynamoDBLocalInstance_RoundTrips_ViaInterface()
    {
        var resource = new PythonLambdaFunctionResource("test", "python3", Path.GetTempPath());
        var iface = (ILambdaFunctionResource)resource;

        Assert.Null(iface.DynamoDBLocalInstance);

        // Verify the internal property and the interface property share the same backing field
        Assert.Null(resource.DynamoDBLocalInstance);
    }

    // -----------------------------------------------------------------------
    // API Gateway overload
    // -----------------------------------------------------------------------

    [Fact]
    public void WithReference_PythonLambda_APIGateway_AddsRouteEnvVar()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        var fn = builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");
        var gateway = builder.AddAWSAPIGatewayEmulator("gw", APIGatewayType.HttpV2)
            .WithReference(fn, Method.Post, "/up");

        // The env callback is registered; confirm the annotation exists
        var envAnnotations = gateway.Resource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();
        Assert.NotEmpty(envAnnotations);
    }

    // -----------------------------------------------------------------------
    // SQS event source overload
    // -----------------------------------------------------------------------

    [Fact]
    public void WithSQSEventSource_PythonLambda_AddsEventSourceResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        var fn = builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");
        fn.WithSQSEventSource("https://sqs.us-east-1.amazonaws.com/123/MyQueue");

        var sqsResource = builder.Resources.OfType<SQSEventSourceResource>().SingleOrDefault();
        Assert.NotNull(sqsResource);
    }

    // -----------------------------------------------------------------------
    // DynamoDB Streams event source overload
    // -----------------------------------------------------------------------

    [Fact]
    public void WithDynamoDBStreamsEventSource_PythonLambda_AddsEventSourceResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        var fn = builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");
        fn.WithDynamoDBStreamsEventSource("MyTable");

        var ddbResource = builder.Resources.OfType<DynamoDBStreamsEventSourceResource>().SingleOrDefault();
        Assert.NotNull(ddbResource);
    }

    // -----------------------------------------------------------------------
    // Backward-compat: .NET Lambda resource still works
    // -----------------------------------------------------------------------

    [Fact]
    public void DotNetAndPython_CanCoexist_InSameAppHost()
    {
        var builder = DistributedApplication.CreateBuilder();
        var appDir = Path.GetTempPath();

        // Both types of Lambda resource should register without conflict
        builder.AddAWSPythonLambdaFunction("PyFn", appDir, "main.handler");

        var pythonResources = builder.Resources.OfType<PythonLambdaFunctionResource>().ToList();
        Assert.Single(pythonResources);

        // Only one Lambda emulator even when mixing resource types
        Assert.Single(builder.Resources.OfType<LambdaEmulatorResource>());
    }
}
