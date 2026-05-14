// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

public class DefaultStaticSiteBuilderTests
{
    [Fact]
    public async Task BuildAsync_NoAnnotations_RunsNpmRunBuild()
    {
        var (mock, builder, resource) = Setup();
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(0);

        await builder.BuildAsync(resource, resource.WorkingDirectory, new Dictionary<string, string>(), CancellationToken.None);

        mock.Verify(p => p.RunProcess(
            It.IsAny<Microsoft.Extensions.Logging.ILogger>(),
            "npm",
            "run build",
            resource.WorkingDirectory,
            true,
            It.IsAny<IDictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_PackageManagerAnnotation_UsesAnnotatedExecutable()
    {
        var (mock, builder, resource) = Setup();
        resource.Annotations.Add(new JavaScriptPackageManagerAnnotation("yarn", "run", null));
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(0);

        await builder.BuildAsync(resource, resource.WorkingDirectory, new Dictionary<string, string>(), CancellationToken.None);

        mock.Verify(p => p.RunProcess(
            It.IsAny<Microsoft.Extensions.Logging.ILogger>(),
            "yarn",
            "run build",
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_BuildScriptAnnotation_UsesAnnotatedScriptName()
    {
        var (mock, builder, resource) = Setup();
        resource.Annotations.Add(new JavaScriptBuildScriptAnnotation("compile", null));
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(0);

        await builder.BuildAsync(resource, resource.WorkingDirectory, new Dictionary<string, string>(), CancellationToken.None);

        mock.Verify(p => p.RunProcess(
            It.IsAny<Microsoft.Extensions.Logging.ILogger>(),
            "npm",
            "run compile",
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_BuildScriptAnnotationWithArgs_AppendsDashDashArgs()
    {
        var (mock, builder, resource) = Setup();
        resource.Annotations.Add(new JavaScriptBuildScriptAnnotation("build", ["--mode", "production"]));
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(0);

        await builder.BuildAsync(resource, resource.WorkingDirectory, new Dictionary<string, string>(), CancellationToken.None);

        mock.Verify(p => p.RunProcess(
            It.IsAny<Microsoft.Extensions.Logging.ILogger>(),
            "npm",
            "run build -- --mode production",
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_PassesEnvironmentVariablesToProcess()
    {
        var (mock, builder, resource) = Setup();
        var envVars = new Dictionary<string, string> { ["VITE_API_URL"] = "https://api.example.com" };
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(0);

        await builder.BuildAsync(resource, resource.WorkingDirectory, envVars, CancellationToken.None);

        mock.Verify(p => p.RunProcess(
            It.IsAny<Microsoft.Extensions.Logging.ILogger>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            envVars), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_NonZeroExitCode_ThrowsInvalidOperationException()
    {
        var (mock, builder, resource) = Setup();
        mock.Setup(p => p.RunProcess(It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns(1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.BuildAsync(resource, resource.WorkingDirectory, new Dictionary<string, string>(), CancellationToken.None));

        Assert.Contains("frontend", ex.Message);
        Assert.Contains("exit code 1", ex.Message);
    }

    private static (Mock<IProcessCommandService> Mock, DefaultStaticSiteBuilder Builder, JavaScriptAppResource Resource) Setup()
    {
        var mock = new Mock<IProcessCommandService>();
        var builder = new DefaultStaticSiteBuilder(mock.Object, NullLogger<DefaultStaticSiteBuilder>.Instance);
        var resource = new JavaScriptAppResource("frontend", "npm", Path.GetTempPath());
        return (mock, builder, resource);
    }
}
