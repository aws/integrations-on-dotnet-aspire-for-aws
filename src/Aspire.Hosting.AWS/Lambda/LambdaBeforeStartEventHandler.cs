// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Handles the subscription and processing of events that occur before a Lambda resource starts within a distributed
/// application environment.
/// </summary>
/// <remarks>This event handler is responsible for configuring Lambda project resources prior to their startup,
/// including validation and setup of local emulation tools when required. It supports both IDE and non-IDE scenarios
/// for Lambda function execution and ensures that necessary dependencies are installed and configured. This class is
/// intended for internal use within the distributed application eventing infrastructure.</remarks>
/// <param name="logger">The logger used to record diagnostic and operational information during event handling.</param>
/// <param name="processCommandService">The service used to execute and manage external process commands required for Lambda resource preparation.</param>
/// <param name="executionContext">The execution context representing the current distributed application run mode and environment.</param>
internal class LambdaBeforeStartEventHandler(ILogger<LambdaEmulatorResource> logger, IProcessCommandService processCommandService, DistributedApplicationExecutionContext executionContext) : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeResourceStartedAsync);
        return Task.CompletedTask;
    }

    public async Task OnBeforeResourceStartedAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        if (!executionContext.IsRunMode)
        {
            return;
        }

        SdkUtilities.BackgroundSDKDefaultConfigValidation(logger);

        // Set up Python virtual environments for any Python Lambda functions.
        var pythonResources = @event.Model.Resources
            .OfType<PythonLambdaFunctionResource>()
            .ToList();

        foreach (var pythonResource in pythonResources)
        {
            await SetupPythonVirtualEnvironmentAsync(pythonResource, cancellationToken);
        }

        // The Lambda function handler for a Class Library contains "::".
        // This is an example of a class library function handler "WebCalculatorFunctions::WebCalculatorFunctions.Functions::AddFunctionHandler".
        var classLibraryProjectPaths =
            @event.Model.Resources
                .OfType<LambdaProjectResource>()
                .Where(x =>
                {
                    if (!x.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var functionAnnotation))
                        return false;

                    if (!functionAnnotation.Handler.Contains("::"))
                        return false;

                    return true;
                })
                .ToList();

        LambdaEmulatorAnnotation? emulatorAnnotation = null;
        if (@event.Model.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out emulatorAnnotation)) != null && emulatorAnnotation != null)
        {
            await ApplyLambdaEmulatorAnnotationAsync(emulatorAnnotation, cancellationToken);

            foreach (var projectResource in classLibraryProjectPaths)
            {
                var projectMetadata = projectResource.Annotations
                    .OfType<IProjectMetadata>()
                    .First();
                var lambdaFunctionAnnotation = projectResource.Annotations
                    .OfType<LambdaFunctionAnnotation>()
                    .First();

                // If we are running Aspire through an IDE where a debugger is attached,
                // we want to configure the Aspire resource to use a Launch Setting Profile that will be able to run the class library Lambda function.
                if (AspireUtilities.IsRunningInDebugger)
                {
                    var installPath = await GetCurrentInstallPathAsync(cancellationToken);
                    if (string.IsNullOrEmpty(installPath))
                    {
                        logger.LogError("Failed to determine The location of Amazon.Lambda.TestTool on disk which is required for running class library Lambda functions.");
                        return;
                    }
                    var contentFolder = new DirectoryInfo(installPath).Parent?.Parent?.Parent?.FullName;
                    if (string.IsNullOrEmpty(contentFolder))
                    {
                        logger.LogError("Failed to determine the content folder of Amazon.Lambda.TestTool NuGet package which is required for running class library Lambda functions.");
                        return;
                    }
                    var targetFramework = await GetProjectTargetFrameworkAsync(projectMetadata.ProjectPath, cancellationToken);
                    if (string.IsNullOrEmpty(targetFramework))
                    {
                        logger.LogError("Cannot determine the target framework of the project '{ProjectPath}'", projectMetadata.ProjectPath);
                        continue;
                    }
                    var assemblyName = await GetProjectAssemblyNameAsync(projectMetadata.ProjectPath, cancellationToken);
                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        logger.LogError("Cannot determine the assembly name of the project '{ProjectPath}'", projectMetadata.ProjectPath);
                        continue;
                    }
                    var runtimeSupportAssemblyPath = Path.Combine(contentFolder, "content", "Amazon.Lambda.RuntimeSupport",
                        targetFramework, "Amazon.Lambda.RuntimeSupport.TestTool.dll");
                    if (!File.Exists(runtimeSupportAssemblyPath))
                    {
                        // The test tool renames Amazon.Lambda.RuntimeSupport.dll to Amazon.Lambda.RuntimeSupport.TestTool.dll to avoid the version of
                        // Amazon.Lambda.RuntimeSupport.dll from the Lambda project (if referenced via NuGet) being used instead. Older versions of the test tool
                        // do not perform this rename, so this check is to see if we can find the non-renamed version if we didn't find the renamed version.
                        runtimeSupportAssemblyPath = runtimeSupportAssemblyPath.Replace("Amazon.Lambda.RuntimeSupport.TestTool.dll", "Amazon.Lambda.RuntimeSupport.dll");
                        if (!File.Exists(runtimeSupportAssemblyPath))
                        {
                            logger.LogError("Cannot find a version of Amazon.Lambda.RuntimeSupport that supports your project's target framework '{Framework}'. The following file does not exist '{RuntimeSupportPath}'.", targetFramework, runtimeSupportAssemblyPath);
                            continue;
                        }
                    }

                    var outputPath = await GetProjectOutputPathAsync(projectMetadata.ProjectPath, cancellationToken);
                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        outputPath = Path.Combine(".", "bin", "$(Configuration)", targetFramework);
                    }

                    ProjectUtilities.UpdateLaunchSettingsWithLambdaTester(
                        resourceName: projectResource.Name,
                        functionHandler: lambdaFunctionAnnotation.Handler,
                        assemblyName: assemblyName,
                        projectPath: projectMetadata.ProjectPath,
                        runtimeSupportAssemblyPath: runtimeSupportAssemblyPath,
                        targetFramework: targetFramework,
                        outputPath: outputPath,
                        logger: logger);
                }
                // If we are running outside an IDE, the Launch Setting Profile approach does not work.
                // We need to create a wrapper executable project that runs the class library project and add the wrapper project as the Aspire resource.
                else
                {
                    var targetFramework = await GetProjectTargetFrameworkAsync(projectMetadata.ProjectPath, cancellationToken);
                    if (string.IsNullOrEmpty(targetFramework))
                    {
                        logger.LogError("Cannot determine the target framework of the project '{ProjectPath}'", projectMetadata.ProjectPath);
                        continue;
                    }

                    projectResource.Annotations.Remove(projectMetadata);

                    var projectPath =
                        ProjectUtilities.CreateExecutableWrapperProject(projectMetadata.ProjectPath, lambdaFunctionAnnotation.Handler, targetFramework);

                    projectResource.Annotations.Add(new LambdaProjectMetadata(projectPath));

                    var projectName = new FileInfo(projectPath).Name;
                    var workingDirectory = Directory.GetParent(projectPath)!.FullName;
                    processCommandService.RunProcess(logger, "dotnet", $"build {projectName}", workingDirectory, streamOutputToLogger: false);
                    processCommandService.RunProcess(logger, "dotnet", $"build -c Release {projectName}", workingDirectory, streamOutputToLogger: false);
                }
            }
        }
        else
        {
            logger.LogDebug("Skipping installing Amazon.Lambda.TestTool since no Lambda emulator resource was found");
        }
    }

    internal async Task ApplyLambdaEmulatorAnnotationAsync(LambdaEmulatorAnnotation emulatorAnnotation, CancellationToken cancellationToken = default)
    {
        if (emulatorAnnotation.DisableAutoInstall)
        {
            return;
        }

        var expectedVersion = emulatorAnnotation.OverrideMinimumInstallVersion ?? Constants.DefaultLambdaTestToolVersion;
        var installedVersion = await GetCurrentInstalledVersionAsync(cancellationToken);

        if (ShouldInstall(installedVersion, expectedVersion, emulatorAnnotation.AllowDowngrade))
        {
            logger.LogDebug("Installing .NET Tool Amazon.Lambda.TestTool ({version})", expectedVersion);

            var commandLineArgument = $"tool install -g Amazon.Lambda.TestTool --version {expectedVersion}";
            if (emulatorAnnotation.AllowDowngrade)
            {
                commandLineArgument += " --allow-downgrade";
            }

            var result = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", commandLineArgument, null, cancellationToken);
            if (result.ExitCode == 0)
            {
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    logger.LogInformation("Successfully Updated .NET Tool Amazon.Lambda.TestTool from version {installedVersion} to {newVersion}", installedVersion, expectedVersion);
                }
                else
                {
                    logger.LogInformation("Successfully installed .NET Tool Amazon.Lambda.TestTool ({version})", expectedVersion);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(installedVersion))
                {
                    logger.LogWarning("Failed to update Amazon.Lambda.TestTool from {installedVersion} to {expectedVersion}:\n{output}", installedVersion, expectedVersion, result.Output);
                }
                else
                {
                    logger.LogError("Fail to install Amazon.Lambda.TestTool ({version}) required for running Lambda functions locally:\n{output}", expectedVersion, result.Output);
                }
            }
        }
        else
        {
            logger.LogInformation("Amazon.Lambda.TestTool version {version} already installed", installedVersion);
        }
    }

    internal static bool ShouldInstall(string currentInstalledVersion, string expectedVersionStr, bool allowDowngrading)
    {
        if (string.IsNullOrEmpty(currentInstalledVersion))
        {
            return true;
        }

        var installedVersion = Version.Parse(currentInstalledVersion.Replace("-preview", string.Empty));
        var expectedVersion = Version.Parse(expectedVersionStr.Replace("-preview", string.Empty));

        return (installedVersion < expectedVersion) || (allowDowngrading && installedVersion != expectedVersion);
    }

    private async Task<string> GetCurrentInstalledVersionAsync(CancellationToken cancellationToken)
    {
        var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", "lambda-test-tool info --format json", null, cancellationToken);
        if (results.ExitCode != 0)
        {
            return string.Empty;
        }

        try
        {
            var versionDoc = JsonNode.Parse(results.Output);
            if (versionDoc == null)
            {
                logger.LogWarning("Error parsing version information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
                return string.Empty;

            }
            var version = versionDoc["Version"]?.ToString();
            logger.LogDebug("Installed version of Amazon.Lambda.TestTool is {version}", version);
            return version ?? string.Empty;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error parsing version information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
            return string.Empty;
        }
    }

    internal async Task<string> GetCurrentInstallPathAsync(CancellationToken cancellationToken)
    {
        var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", "lambda-test-tool info --format json", null, cancellationToken);
        if (results.ExitCode != 0)
        {
            return string.Empty;
        }

        try
        {
            var installPathDoc = JsonNode.Parse(results.Output);
            if (installPathDoc == null)
            {
                logger.LogWarning("Error parsing install path information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
                return string.Empty;

            }
            var installPath = installPathDoc["InstallPath"]?.ToString();
            logger.LogDebug("Install path of Amazon.Lambda.TestTool is {version}", installPath);
            return installPath ?? string.Empty;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error parsing install path information from Amazon.Lambda.TestTool: {versionInfo}", results.Output);
            return string.Empty;
        }
    }

    internal async Task<string> GetProjectAssemblyNameAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:AssemblyName", null, cancellationToken);
            if (results.ExitCode != 0)
            {
                return string.Empty;
            }

            logger.LogDebug("The assembly name of '{projectPath}' is {assemblyName}", projectPath, results.Output);
            return results.Output.Trim();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error retrieving the assembly name of '{projectPath}'", projectPath);
            return string.Empty;
        }
    }

    internal async Task<string> GetProjectOutputPathAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:OutputPath", null, cancellationToken);
            if (results.ExitCode != 0)
            {
                return string.Empty;
            }

            logger.LogDebug("The output path of '{projectPath}' is {outputPath}", projectPath, results.Output);
            return results.Output.Trim();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error retrieving the output path of '{projectPath}'", projectPath);
            return string.Empty;
        }
    }


    internal async Task<string> GetProjectTargetFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            var results = await processCommandService.RunProcessAndCaptureOutputAsync(logger, "dotnet", $"msbuild \"{projectPath}\" -nologo -v:q -getProperty:TargetFramework", null, cancellationToken);
            if (results.ExitCode != 0)
            {
                return string.Empty;
            }

            logger.LogDebug("The target framework of '{projectPath}' is {targetFramework}", projectPath, results.Output);
            return results.Output.Trim();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Error retrieving the target framework of '{projectPath}'", projectPath);
            return string.Empty;
        }
    }

    private async Task SetupPythonVirtualEnvironmentAsync(PythonLambdaFunctionResource resource, CancellationToken cancellationToken)
    {
        if (!resource.TryGetLastAnnotation<PythonVirtualEnvironmentAnnotation>(out var venvAnnotation) || venvAnnotation == null)
        {
            return;
        }

        var venvPath = venvAnnotation.VirtualEnvironmentPath;
        var venvPython = OperatingSystem.IsWindows()
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python");

        if (!File.Exists(venvPython))
        {
            if (!venvAnnotation.CreateIfNotExists)
            {
                logger.LogError(
                    "Python virtual environment not found at '{VenvPath}' for Lambda function '{ResourceName}'. " +
                    "Create it manually or set createIfNotExists: true on WithVirtualEnvironment.",
                    venvPath, resource.Name);
                return;
            }

            logger.LogInformation("Creating Python virtual environment at '{VenvPath}' for Lambda function '{ResourceName}'...", venvPath, resource.Name);

            var systemPython = string.IsNullOrWhiteSpace(venvAnnotation.PythonExecutablePath)
                ? (OperatingSystem.IsWindows() ? "python" : "python3")
                : venvAnnotation.PythonExecutablePath;
            var createResult = await processCommandService.RunProcessAndCaptureOutputAsync(
                logger, systemPython, $"-m venv \"{venvPath}\"", resource.AppDirectory, cancellationToken);

            if (createResult.ExitCode != 0)
            {
                logger.LogError("Failed to create virtual environment at '{VenvPath}':\n{Output}", venvPath, createResult.Output);
                return;
            }

            logger.LogInformation("Virtual environment created at '{VenvPath}'.", venvPath);
        }

        // Now that the venv exists, update the resource's executable to use it.
        venvAnnotation.ResourceBuilder?.WithCommand(venvPython);

        // Install requirements.txt if present.
        var requirementsFile = Path.Combine(resource.AppDirectory, "requirements.txt");
        if (File.Exists(requirementsFile))
        {
            var pip = OperatingSystem.IsWindows()
                ? Path.Combine(venvPath, "Scripts", "pip.exe")
                : Path.Combine(venvPath, "bin", "pip");

            logger.LogInformation("Installing requirements from '{RequirementsFile}' for Lambda function '{ResourceName}'...", requirementsFile, resource.Name);

            var installResult = await processCommandService.RunProcessAndCaptureOutputAsync(
                logger, pip, $"install -r \"{requirementsFile}\"", resource.AppDirectory, cancellationToken);

            if (installResult.ExitCode != 0)
            {
                logger.LogError("Failed to install requirements for Lambda function '{ResourceName}':\n{Output}", resource.Name, installResult.Output);
            }
            else
            {
                logger.LogInformation("Requirements installed successfully for Lambda function '{ResourceName}'.", resource.Name);
            }
        }

        // Write the Aspire Lambda bootstrap script and set it as the entry point.
        // The bootstrap is a pure Python implementation of the Lambda Runtime API client
        // that works on all platforms (including macOS), unlike awslambdaric's C extension
        // which is Linux-only.
        var bootstrapPath = Path.Combine(resource.AppDirectory, "_aspire_lambda_bootstrap.py");
        await File.WriteAllTextAsync(bootstrapPath, AspireBootstrapScript, cancellationToken);
        venvAnnotation.ResourceBuilder?.WithArgs(bootstrapPath, resource.Handler);

        logger.LogDebug("Aspire Lambda bootstrap written to '{BootstrapPath}' for Lambda function '{ResourceName}'.", bootstrapPath, resource.Name);
    }

    /// <summary>
    /// Pure Python Lambda Runtime API client bootstrap.
    /// Replaces <c>awslambdaric</c>'s C extension (<c>runtime_client</c>), which is Linux-only,
    /// with a stdlib <c>http.client</c> implementation that works on macOS and Linux alike.
    /// </summary>
    private const string AspireBootstrapScript = """"
        #!/usr/bin/env python3
        # Aspire local Lambda bootstrap.
        # Pure Python implementation of the AWS Lambda Runtime Interface for local development.
        # Works on macOS and Linux without requiring the awslambdaric C extension.
        import os, sys, json, importlib, traceback, http.client


        class _Context:
            def __init__(self, hdrs):
                self.aws_request_id          = hdrs.get("lambda-runtime-aws-request-id", "")
                self.invoked_function_arn     = hdrs.get("lambda-runtime-invoked-function-arn", "")
                self.function_name            = os.environ.get("AWS_LAMBDA_FUNCTION_NAME", "local-function")
                self.function_version         = os.environ.get("AWS_LAMBDA_FUNCTION_VERSION", "$LATEST")
                self.memory_limit_in_mb       = int(os.environ.get("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", "128"))
                self.deadline_time_in_ms      = int(hdrs.get("lambda-runtime-deadline-ms") or "0")
            def get_remaining_time_in_millis(self):
                import time; return max(0, self.deadline_time_in_ms - int(time.time() * 1000))


        def _req(addr, method, path, body=None, extra_headers=None):
            conn = http.client.HTTPConnection(addr)
            headers = {"Content-Type": "application/json"}
            if extra_headers:
                headers.update(extra_headers)
            conn.request(method, path, body=body, headers=headers)
            resp = conn.getresponse()
            data = resp.read()
            hdrs = {k.lower(): v for k, v in resp.getheaders()}
            conn.close()
            return data, hdrs


        def _load(handler_str):
            dot = handler_str.rfind(".")
            if dot < 0:
                raise ValueError(f"Invalid handler '{handler_str}': expected module.function")
            mod = importlib.import_module(handler_str[:dot])
            return getattr(mod, handler_str[dot + 1:])


        def main(addr, prefix, handler_str):
            # Build paths with the optional prefix (e.g. "/ToUpperFunction")
            # AWS_LAMBDA_RUNTIME_API may be "host:port" or "host:port/path-prefix"
            p_next     = f"{prefix}/2018-06-01/runtime/invocation/next"
            p_resp     = f"{prefix}/2018-06-01/runtime/invocation/" + "{}/response"
            p_err      = f"{prefix}/2018-06-01/runtime/invocation/" + "{}/error"
            p_init_err = f"{prefix}/2018-06-01/runtime/init/error"

            print(f"[Aspire Bootstrap] Runtime API: {addr}{prefix}", file=sys.stderr, flush=True)
            print(f"[Aspire Bootstrap] Handler:     {handler_str}", file=sys.stderr, flush=True)
            try:
                fn = _load(handler_str)
            except Exception as exc:
                tb = traceback.format_exc()
                print(f"[Aspire Bootstrap] Failed to load handler: {tb}", file=sys.stderr)
                payload = json.dumps({"errorType": type(exc).__name__, "errorMessage": str(exc), "stackTrace": tb.splitlines()}).encode()
                try:
                    _req(addr, "POST", p_init_err, payload, {"Lambda-Runtime-Function-Error-Type": type(exc).__name__})
                except Exception:
                    pass
                sys.exit(1)

            print("[Aspire Bootstrap] Ready - waiting for invocations.", file=sys.stderr, flush=True)
            while True:
                try:
                    body, hdrs = _req(addr, "GET", p_next)
                except Exception as exc:
                    print(f"[Aspire Bootstrap] Failed to get next invocation: {exc}", file=sys.stderr)
                    sys.exit(1)

                req_id = hdrs.get("lambda-runtime-aws-request-id", "unknown")
                ctx    = _Context(hdrs)
                try:
                    event = json.loads(body) if body else {}
                except Exception:
                    event = body.decode("utf-8", errors="replace") if isinstance(body, bytes) else str(body)

                try:
                    result = fn(event, ctx)
                    _req(addr, "POST", p_resp.format(req_id), json.dumps(result).encode())
                except Exception as exc:
                    tb = traceback.format_exc()
                    print(f"[Aspire Bootstrap] Handler error:\n{tb}", file=sys.stderr, flush=True)
                    payload = json.dumps({"errorType": type(exc).__name__, "errorMessage": str(exc), "stackTrace": tb.splitlines()}).encode()
                    _req(addr, "POST", p_err.format(req_id), payload, {"Lambda-Runtime-Function-Error-Type": type(exc).__name__})


        if __name__ == "__main__":
            if len(sys.argv) < 2:
                print("Usage: _aspire_lambda_bootstrap.py <handler>", file=sys.stderr)
                sys.exit(1)
            _raw = os.environ.get("AWS_LAMBDA_RUNTIME_API")
            if not _raw:
                print("[Aspire Bootstrap] ERROR: AWS_LAMBDA_RUNTIME_API is not set.", file=sys.stderr)
                sys.exit(1)
            # AWS_LAMBDA_RUNTIME_API may be "host:port" or "host:port/path-prefix"
            _slash = _raw.find("/")
            if _slash >= 0:
                _addr, _prefix = _raw[:_slash], _raw[_slash:]
            else:
                _addr, _prefix = _raw, ""
            main(_addr, _prefix, sys.argv[1])
        """";
}

internal sealed class LambdaProjectMetadata(string projectPath) : IProjectMetadata
{
    public string ProjectPath { get; } = projectPath;

    // The wrapper project is already built explicitly before DCP starts it.
    // Setting SuppressBuild=true causes Aspire to pass --no-build to `dotnet run`,
    // preventing an incremental build that skips GenerateBuildRuntimeConfigurationFiles
    // and leaves runtimeconfig.json missing on first launch.
    public bool SuppressBuild => true;
}
