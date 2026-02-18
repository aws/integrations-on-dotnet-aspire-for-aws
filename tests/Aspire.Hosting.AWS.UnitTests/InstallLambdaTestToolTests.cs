// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Aspire.Hosting.AWS.UnitTests;

#pragma warning disable CA2252
public class InstallLambdaTestToolTests
{
    [Fact]
    public async Task InstallWithNothingCurrentlyInstalled()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult (-404, "Command not found" ),
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Installed successfully")
                );

        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()));

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json",
            $"tool install -g Amazon.Lambda.TestTool --version {Constants.DefaultLambdaTestToolVersion}"
            );
    }

    [Fact]
    public async Task ToolAlreadyInstalled()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, GenerateVersionJson(Constants.DefaultLambdaTestToolVersion))
                );

        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()));

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json"
            );
    }

    [Fact]
    public async Task ToolNeedsToUpdated()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, GenerateVersionJson("0.0.1-preview")),
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Installed successfully")
                );

        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()));

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json",
            $"tool install -g Amazon.Lambda.TestTool --version {Constants.DefaultLambdaTestToolVersion}"
            );
    }

    [Fact]
    public async Task NewerVersionAlreadyInstalled()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, GenerateVersionJson("99.99.99")),
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Installed successfully")
                );


        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()));

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json"
            );
    }

    [Fact]
    public async Task OverrideVersionToNewerVersion()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, GenerateVersionJson(Constants.DefaultLambdaTestToolVersion)),
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Installed successfully")
                );

        const string overrideVersion = "99.99.99";
        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()) { OverrideMinimumInstallVersion = overrideVersion});

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json",
            $"tool install -g Amazon.Lambda.TestTool --version {overrideVersion}"
            );
    }

    [Fact]
    public async Task DisableAutoInstall()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService();

        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()) { DisableAutoInstall = true });

        processCommandService.AssertCommands();
    }

    [Fact]
    public async Task AllowDowngrading()
    {
        var loggerMock = new Mock<ILogger<LambdaEmulatorResource>>();

        var processCommandService = new MockProcessCommandService(
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, GenerateVersionJson("99.99.99")),
                new IProcessCommandService.RunProcessAndCaptureStdOutResult(0, "Installed successfully")
                );

        var lambdaHook = new LambdaBeforeStartEventHandler(loggerMock.Object, processCommandService, new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        await lambdaHook.ApplyLambdaEmulatorAnnotationAsync(new LambdaEmulatorAnnotation(CreateFakeEndpointReference()) { AllowDowngrade = true });

        processCommandService.AssertCommands(
            "lambda-test-tool info --format json",
            $"tool install -g Amazon.Lambda.TestTool --version {Constants.DefaultLambdaTestToolVersion} --allow-downgrade"
            );
    }

    private EndpointReference CreateFakeEndpointReference() => new EndpointReference(new Mock<IResourceWithEndpoints>().Object, "http");

    private string GenerateVersionJson(string toolVersion) => $"{{\"Version\":\"{toolVersion}\"}}";

    public class MockProcessCommandService(params IProcessCommandService.RunProcessAndCaptureStdOutResult[] results) : IProcessCommandService
    {

        public int CallCount { get; private set; }

        public IList<Tuple<string, string>> CommandsExecuted { get; } = new List<Tuple<string, string>>();

        public Task<IProcessCommandService.RunProcessAndCaptureStdOutResult> RunProcessAndCaptureOutputAsync(ILogger logger, string path, string arguments, string? workingDirectory, CancellationToken cancellationToken)
        {
            if (CallCount == results.Length)
            {
                throw new InvalidOperationException("The process command was called more times than expected");
            }
            CommandsExecuted.Add(new Tuple<string, string>(path, arguments));

            var result = results[CallCount];
            CallCount++;
            return Task.FromResult(result);
        }

        public int RunProcess(ILogger logger, string path, string arguments, string workingDirectory, bool streamOutputToLogger, IDictionary<string, string>? environmentVariables = null)
        {
            if (CallCount == results.Length)
            {
                throw new InvalidOperationException("The process command was called more times than expected");
            }
            CommandsExecuted.Add(new Tuple<string, string>(path, arguments));

            var result = results[CallCount];
            CallCount++;
            return result.ExitCode;
        }

        public void AssertCommands(params string[] commandArguments)
        {
            Assert.Equal(commandArguments.Length, CommandsExecuted.Count);

            for (int i = 0; i < commandArguments.Length; i++)
            {
                Assert.Equal("dotnet", CommandsExecuted[i].Item1);
                Assert.Equal(commandArguments[i], CommandsExecuted[i].Item2);
            }
        }

        public IProcessCommandService.RunProcessAndCaptureStdOutResult RunCDKProcess(ILogger? logger, LogLevel logLevel, string arguments, string workingDirectory, IDictionary<string, string>? environmentVariables = null)
        {
            throw new NotImplementedException();
        }
    }
}
