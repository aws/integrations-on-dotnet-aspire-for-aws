using System.Text.Json.Nodes;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class LambdaLifecycleHookTests
{
    private Mock<IProcessCommandService> _processCommandService = new Mock<IProcessCommandService>();
    private Mock<ILogger<LambdaEmulatorResource>> _logger = new Mock<ILogger<LambdaEmulatorResource>>();
    
    [Fact]
    public async Task GetCurrentInstallPathAsync_ReturnsInstallPath_WhenValidJsonOutput()
    {
        // Arrange
        string expectedInstallPath = Path.GetTempPath();

        // Escape Windows forward slashes when injecting the value into the JSON document.
        string jsonOutput = JsonNode.Parse($"{{ \"InstallPath\": \"{expectedInstallPath.Replace("\\", "\\\\")}\" }}")!.ToJsonString();

        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                _logger.Object,
                "dotnet",
                "lambda-test-tool info --format json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, jsonOutput));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);

        // Act
        string result = await hook.GetCurrentInstallPathAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectedInstallPath, result);
    }

    [Fact]
    public async Task GetCurrentInstallPathAsync_ReturnsEmptyString_WhenExitCodeNonZero()
    {
        // Arrange
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                "lambda-test-tool info --format json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(1, "Error"));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);

        // Act
        string result = await hook.GetCurrentInstallPathAsync(CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetCurrentInstallPathAsync_ReturnsEmptyString_WhenJsonIsMalformed()
    {
        // Arrange
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                "lambda-test-tool info --format json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Not a JSON"));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);

        // Act
        string result = await hook.GetCurrentInstallPathAsync(CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetProjectAssemblyNameAsync_ReturnsAssemblyName_WhenProcessSucceeds()
    {
        // Arrange
        string expectedAssemblyName = "MyTestAssembly";
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                It.Is<string>(args => args.Contains("-getProperty:AssemblyName")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, expectedAssemblyName));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);
        string dummyProjectPath = "dummy.csproj";

        // Act
        string result = await hook.GetProjectAssemblyNameAsync(dummyProjectPath, CancellationToken.None);

        // Assert
        Assert.Equal(expectedAssemblyName, result);
    }

    [Fact]
    public async Task GetProjectAssemblyNameAsync_ReturnsEmptyString_WhenExitCodeNonZero()
    {
        // Arrange
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                It.Is<string>(args => args.Contains("-getProperty:AssemblyName")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(1, "Error"));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);
        string dummyProjectPath = "dummy.csproj";

        // Act
        string result = await hook.GetProjectAssemblyNameAsync(dummyProjectPath, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetProjectTargetFrameworkAsync_ReturnsTargetFramework_WhenProcessSucceeds()
    {
        // Arrange
        string expectedTargetFramework = "net6.0";
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                It.Is<string>(args => args.Contains("-getProperty:TargetFramework")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, expectedTargetFramework));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);
        string dummyProjectPath = "dummy.csproj";

        // Act
        string result = await hook.GetProjectTargetFrameworkAsync(dummyProjectPath, CancellationToken.None);

        // Assert
        Assert.Equal(expectedTargetFramework, result);
    }

    [Fact]
    public async Task GetProjectTargetFrameworkAsync_ReturnsEmptyString_WhenExitCodeNonZero()
    {
        // Arrange
        _processCommandService
            .Setup(s => s.RunProcessAndCaptureOuputAsync(
                It.IsAny<ILogger>(),
                "dotnet",
                It.Is<string>(args => args.Contains("-getProperty:TargetFramework")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IProcessCommandService.RunProcessAndCaptureStdOutResult(1, "Error"));

        var hook = new LambdaLifecycleHook(_logger.Object, _processCommandService.Object);
        string dummyProjectPath = "dummy.csproj";

        // Act
        string result = await hook.GetProjectTargetFrameworkAsync(dummyProjectPath, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, result);
    }
}