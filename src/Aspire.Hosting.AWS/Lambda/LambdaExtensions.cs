// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Lambda functions as Aspire resources.
/// </summary>
public static class LambdaExtensions
{   
    /// <summary>
    /// Add a Lambda function as an Aspire resource.
    /// </summary>
    /// <typeparam name="TLambdaProject"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name">Aspire resource name</param>
    /// <param name="lambdaHandler">Lambda function handler</param>
    /// <returns></returns>
    public static IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunction<TLambdaProject>(this IDistributedApplicationBuilder builder, string name, string lambdaHandler, LambdaFunctionOptions? options = null) where TLambdaProject : IProjectMetadata, new()
    {
        options ??= new LambdaFunctionOptions();
        var metadata = new TLambdaProject();

        IResourceBuilder<LambdaProjectResource> resource;
        // The Lambda function handler for a Class Library contains "::".
        // This is an example of a class library function handler "WebCalculatorFunctions::WebCalculatorFunctions.Functions::AddFunctionHandler".
        if (lambdaHandler.Contains("::") && AspireUtilities.IsRunningInDebugger)
        {
            // If we are running Aspire through an IDE where a debugger is attached,
            // we want to configure the Aspire resource to use a Launch Setting Profile that will be able to run the class library Lambda function.
            var project = new LambdaProjectResource(name);
            resource = builder.AddResource(project)
                .WithAnnotation(new LaunchProfileAnnotation($"{Constants.LaunchSettingsNodePrefix}{name}"))
                .WithAnnotation(new TLambdaProject());
        }
        else
        {
            var project = new LambdaProjectResource(name);
            resource = builder.AddResource(project)
                            .WithAnnotation(new TLambdaProject());
        }

        ExecutableResource? serviceEmulator = null;
        if (builder.ExecutionContext.IsRunMode)
        {
            serviceEmulator = AddOrGetLambdaServiceEmulatorResource(builder);
            resource.WithParentRelationship(serviceEmulator);
        }

        resource.WithOpenTelemetry();

        resource.WithEnvironment(context =>
        {
            // If we are in publishing mode we do not need to connect the Lambda emulator which is only used for local development and testing.
            if (context.ExecutionContext.IsPublishMode || serviceEmulator == null)
                return;

            var serviceRuntimeAPIEndpoint = serviceEmulator.GetEndpoint("http");

            if (!serviceEmulator.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out var lambdaEmulatorAnnotation) || lambdaEmulatorAnnotation == null)
            {
                return;
            }

            // Add the Lambda function resource on the path so the emulator can distinguish request
            // for each Lambda function.
            var apiPath = $"{serviceRuntimeAPIEndpoint.Host}:{serviceRuntimeAPIEndpoint.Port}/{name}";
            context.EnvironmentVariables["AWS_EXECUTION_ENV"] = $"aspire.hosting.aws#{SdkUtilities.GetAssemblyVersion()}";
            context.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = apiPath;
            context.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = name;
            context.EnvironmentVariables["_HANDLER"] = lambdaHandler;

            context.EnvironmentVariables["AWS_LAMBDA_LOG_FORMAT"] = options.LogFormat.Value;
            context.EnvironmentVariables["AWS_LAMBDA_LOG_LEVEL"] = options.ApplicationLogLevel.Value;

            var serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("https");
            if (!serviceEmulatorEndpoint.Exists)
            {
                serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("http");
            }

            var lambdaEmulatorEndpoint = $"{serviceEmulatorEndpoint.Scheme}://{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/?function={Uri.EscapeDataString(name)}";
            
            resource.WithAnnotation(new ResourceCommandAnnotation(
                name: "LambdaEmulator",
                displayName: "Lambda Service Emulator",
                updateState: context =>
                {
                    if (string.Equals(context.ResourceSnapshot.State?.Text, KnownResourceStates.Running))
                    {
                        return ResourceCommandState.Enabled;
                    }
                    return ResourceCommandState.Disabled;
                },
                executeCommand: context =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = lambdaEmulatorEndpoint
                    };
                    Process.Start(startInfo);

                    return Task.FromResult(CommandResults.Success());
                },
                displayDescription: "Open the Lambda service emulator configured for this Lambda function",
                parameter: null,
                confirmationMessage: null,
                iconName: "Bug",
                iconVariant: IconVariant.Filled,
                isHighlighted: true)
            );
        });

        resource.WithAnnotation(new LambdaFunctionAnnotation(lambdaHandler));
        
        return resource;
    }

    /// <summary>
    /// Add a Python Lambda function as an Aspire resource. The function is launched via the
    /// AWS Lambda Runtime Interface Client (<c>python -m awslambdaric</c>) and connected to
    /// the Lambda service emulator, enabling fully local end-to-end development.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="appDirectory">
    /// Path to the directory containing the Python Lambda source code. May be relative to the
    /// AppHost project directory. A <c>.venv</c> virtual environment in this directory is used
    /// if present; otherwise the system <c>python3</c> executable is used. Use
    /// <see cref="WithVirtualEnvironment(Aspire.Hosting.ApplicationModel.IResourceBuilder{Aspire.Hosting.AWS.Lambda.PythonLambdaFunctionResource}, string)"/>
    /// to override the default virtual environment path.
    /// </param>
    /// <param name="handler">
    /// The Lambda function handler in <c>module.function</c> format, for example
    /// <c>main.handler</c> for a <c>handler</c> function inside <c>main.py</c>.
    /// </param>
    /// <param name="options">Optional options to configure the Lambda function.</param>
    /// <returns>A resource builder for the Python Lambda function.</returns>
    public static IResourceBuilder<PythonLambdaFunctionResource> AddAWSPythonLambdaFunction(
        this IDistributedApplicationBuilder builder,
        string name,
        string appDirectory,
        string handler,
        LambdaFunctionOptions? options = null)
    {
        options ??= new LambdaFunctionOptions();

        if (!Path.IsPathRooted(appDirectory))
        {
            appDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, appDirectory));
        }

        var executablePath = ResolvePythonExecutable(appDirectory);
        var resource = new PythonLambdaFunctionResource(name, executablePath, appDirectory, handler);

        var resourceBuilder = builder.AddResource(resource);

        // Attach the default .venv annotation so LambdaBeforeStartEventHandler can
        // create the venv and install requirements.txt on first run.
        var defaultVenvPath = Path.GetFullPath(Path.Combine(appDirectory, ".venv"));
        resourceBuilder.WithAnnotation(new PythonVirtualEnvironmentAnnotation(
            defaultVenvPath,
            createIfNotExists: true,
            pythonExecutablePath: GetSystemPythonExecutable())
        {
            ResourceBuilder = resourceBuilder
        });

        ExecutableResource? serviceEmulator = null;
        if (builder.ExecutionContext.IsRunMode)
        {
            serviceEmulator = AddOrGetLambdaServiceEmulatorResource(builder);
            resourceBuilder.WithParentRelationship(serviceEmulator);
        }

        resourceBuilder.WithEnvironment(context =>
        {
            if (context.ExecutionContext.IsPublishMode || serviceEmulator == null)
                return;

            var serviceRuntimeAPIEndpoint = serviceEmulator.GetEndpoint("http");

            if (!serviceEmulator.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out var lambdaEmulatorAnnotation) || lambdaEmulatorAnnotation == null)
            {
                return;
            }

            var apiPath = $"{serviceRuntimeAPIEndpoint.Host}:{serviceRuntimeAPIEndpoint.Port}/{name}";
            context.EnvironmentVariables["AWS_EXECUTION_ENV"] = $"aspire.hosting.aws#{SdkUtilities.GetAssemblyVersion()}";
            context.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = apiPath;
            context.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = name;
            context.EnvironmentVariables["_HANDLER"] = handler;

            context.EnvironmentVariables["AWS_LAMBDA_LOG_FORMAT"] = options.LogFormat.Value;
            context.EnvironmentVariables["AWS_LAMBDA_LOG_LEVEL"] = options.ApplicationLogLevel.Value;

            var serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("https");
            if (!serviceEmulatorEndpoint.Exists)
            {
                serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("http");
            }

            var lambdaEmulatorEndpoint = $"{serviceEmulatorEndpoint.Scheme}://{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/?function={Uri.EscapeDataString(name)}";

            resourceBuilder.WithAnnotation(new ResourceCommandAnnotation(
                name: "LambdaEmulator",
                displayName: "Lambda Service Emulator",
                updateState: context =>
                {
                    if (string.Equals(context.ResourceSnapshot.State?.Text, KnownResourceStates.Running))
                    {
                        return ResourceCommandState.Enabled;
                    }
                    return ResourceCommandState.Disabled;
                },
                executeCommand: context =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = lambdaEmulatorEndpoint
                    };
                    Process.Start(startInfo);

                    return Task.FromResult(CommandResults.Success());
                },
                displayDescription: "Open the Lambda service emulator configured for this Lambda function",
                parameter: null,
                confirmationMessage: null,
                iconName: "Bug",
                iconVariant: IconVariant.Filled,
                isHighlighted: true)
            );
        });

        resourceBuilder.WithAnnotation(new LambdaFunctionAnnotation(handler));

        return resourceBuilder;
    }

    /// <summary>
    /// Configures a Python Lambda function to use a specific virtual environment.
    /// </summary>
    /// <param name="builder">The Python Lambda function builder.</param>
    /// <param name="virtualEnvironmentPath">
    /// Path to the virtual environment directory. Relative paths are resolved from the
    /// Lambda function <see cref="PythonLambdaFunctionResource.AppDirectory"/>.
    /// Defaults to <c>.venv</c>.
    /// </param>
    /// <param name="createIfNotExists">
    /// When <c>true</c> (the default), the virtual environment is created via
    /// <c>python -m venv</c> if it does not already exist, and
    /// <c>pip install -r requirements.txt</c> is run before the Lambda process starts.
    /// Set to <c>false</c> to require the virtual environment to already exist.
    /// </param>
    /// <returns>The Python Lambda function builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="virtualEnvironmentPath"/> is null or whitespace.</exception>
    public static IResourceBuilder<PythonLambdaFunctionResource> WithVirtualEnvironment(
        this IResourceBuilder<PythonLambdaFunctionResource> builder,
        string virtualEnvironmentPath = ".venv",
        bool createIfNotExists = true)
    {
        if (string.IsNullOrWhiteSpace(virtualEnvironmentPath))
        {
            throw new ArgumentException("Virtual environment path must not be null or whitespace.", nameof(virtualEnvironmentPath));
        }

        var resolvedVenvPath = Path.IsPathRooted(virtualEnvironmentPath)
            ? virtualEnvironmentPath
            : Path.GetFullPath(Path.Combine(builder.Resource.AppDirectory, virtualEnvironmentPath));

        var existingAnnotation = builder.Resource.Annotations
            .OfType<PythonVirtualEnvironmentAnnotation>()
            .LastOrDefault();

        builder.WithAnnotation(
            new PythonVirtualEnvironmentAnnotation(
                resolvedVenvPath,
                createIfNotExists,
                existingAnnotation?.PythonExecutablePath ?? GetSystemPythonExecutable())
            {
                ResourceBuilder = builder
            },
            ResourceAnnotationMutationBehavior.Replace);

        // If the venv already exists, wire up the executable immediately.
        // If not and createIfNotExists is true, the before-start handler will create it
        // and update the command via WithCommand before the process launches.
        var venvPython = OperatingSystem.IsWindows()
            ? Path.Combine(resolvedVenvPath, "Scripts", "python.exe")
            : Path.Combine(resolvedVenvPath, "bin", "python");

        if (File.Exists(venvPython))
        {
            builder.WithCommand(venvPython);
        }
        else if (!createIfNotExists)
        {
            throw new InvalidOperationException(
                $"Python executable was not found in virtual environment '{resolvedVenvPath}'. " +
                $"Expected executable at '{venvPython}'. " +
                $"Create the virtual environment first or set createIfNotExists: true.");
        }

        return builder;
    }

    /// <summary>
    /// Configures which Python executable is used to create the virtual environment when
    /// <see cref="WithVirtualEnvironment(Aspire.Hosting.ApplicationModel.IResourceBuilder{Aspire.Hosting.AWS.Lambda.PythonLambdaFunctionResource}, string, bool)"/>
    /// runs with <c>createIfNotExists: true</c>.
    /// </summary>
    /// <param name="builder">The Python Lambda function builder.</param>
    /// <param name="pythonExecutablePath">
    /// Python executable command or absolute path (for example, <c>python3</c> or
    /// <c>/opt/homebrew/bin/python3.12</c>).
    /// </param>
    /// <returns>The Python Lambda function builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pythonExecutablePath"/> is null or whitespace.</exception>
    public static IResourceBuilder<PythonLambdaFunctionResource> WithPythonExecutable(
        this IResourceBuilder<PythonLambdaFunctionResource> builder,
        string pythonExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(pythonExecutablePath))
        {
            throw new ArgumentException("Python executable path must not be null or whitespace.", nameof(pythonExecutablePath));
        }

        var existingAnnotation = builder.Resource.Annotations
            .OfType<PythonVirtualEnvironmentAnnotation>()
            .LastOrDefault();

        var virtualEnvironmentPath = existingAnnotation?.VirtualEnvironmentPath
            ?? Path.GetFullPath(Path.Combine(builder.Resource.AppDirectory, ".venv"));
        var createIfNotExists = existingAnnotation?.CreateIfNotExists ?? true;

        builder.WithAnnotation(
            new PythonVirtualEnvironmentAnnotation(
                virtualEnvironmentPath,
                createIfNotExists,
                pythonExecutablePath)
            {
                ResourceBuilder = builder
            },
            ResourceAnnotationMutationBehavior.Replace);

        return builder;
    }

    /// <summary>
    /// Resolves the Python executable to use for a Lambda function in the given directory.
    /// Prefers <c>.venv/bin/python</c> (Unix) or <c>.venv\Scripts\python.exe</c> (Windows),
    /// and falls back to system python3/python for the default <c>.venv</c> path.
    /// </summary>
    private static string ResolvePythonExecutable(string appDirectory, string virtualEnvironmentPath = ".venv")
    {
        var resolvedVirtualEnvironmentPath = Path.IsPathRooted(virtualEnvironmentPath)
            ? virtualEnvironmentPath
            : Path.GetFullPath(Path.Combine(appDirectory, virtualEnvironmentPath));

        var venvPython = OperatingSystem.IsWindows()
            ? Path.Combine(resolvedVirtualEnvironmentPath, "Scripts", "python.exe")
            : Path.Combine(resolvedVirtualEnvironmentPath, "bin", "python");

        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        if (!string.Equals(virtualEnvironmentPath, ".venv", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Python executable was not found in virtual environment '{resolvedVirtualEnvironmentPath}'. Expected executable at '{venvPython}'.");
        }

        return OperatingSystem.IsWindows() ? "python" : "python3";
    }

    private static string GetSystemPythonExecutable()
    {
        return OperatingSystem.IsWindows() ? "python" : "python3";
    }

    /// already been added. This method only needs to be called if the emulator needs to be customized with the <see cref="LambdaEmulatorOptions"/>. If
    /// this method is called it must be called only once and before any <see cref="AddAWSLambdaFunction"/> calls.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options">The options to configure the emulator with.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if the Lambda service emulator has already been added.</exception>
    public static IResourceBuilder<LambdaEmulatorResource> AddAWSLambdaServiceEmulator(this IDistributedApplicationBuilder builder, LambdaEmulatorOptions? options = null)
    {
        options ??= new LambdaEmulatorOptions();

        if (builder.Resources.FirstOrDefault(x => x.TryGetAnnotationsOfType<LambdaEmulatorAnnotation>(out _)) is ExecutableResource serviceEmulator)
        {
            throw new InvalidOperationException("A Lambda service emulator has already been added. The AddAWSLambdaFunction will add the emulator " +
                "if it hasn't already been added. This method must be called before AddAWSLambdaFunction if the Lambda service emulator needs to be customized.");
        }

        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();

        var lambdaEmulator = builder.AddResource(new LambdaEmulatorResource("LambdaServiceEmulator")).ExcludeFromManifest();
        lambdaEmulator.WithArgs(context =>
        {
            lambdaEmulator.Resource.AddCommandLineArguments(context.Args, options);
        });

        var annotationHttpApi = new EndpointAnnotation(
            protocol: ProtocolType.Tcp,
            uriScheme: "http",
            port: options.HttpPort);

        lambdaEmulator.WithAnnotation(annotationHttpApi);
        var endpointHttpReference = new EndpointReference(lambdaEmulator.Resource, annotationHttpApi);

        EndpointReference? endpointHttpsReference = null;
        if (!options.DisableHttpsEndpoint)
        {
            var annotationUI = new EndpointAnnotation(
                protocol: ProtocolType.Tcp,
                uriScheme: "https",
                port: options.HttpsPort);

            lambdaEmulator.WithAnnotation(annotationUI);
            lambdaEmulator.WithUrlForEndpoint("https", u => u.DisplayText = "Lambda Test Tool UI");
            endpointHttpsReference = new EndpointReference(lambdaEmulator.Resource, annotationUI);
        }
        else
        {
        lambdaEmulator.WithUrlForEndpoint("http", u => u.DisplayText = "Lambda Test Tool UI");
        }

        lambdaEmulator.WithAnnotation(new LambdaEmulatorAnnotation(lambdaRuntimeEndpoint: endpointHttpReference)
        {
            DisableAutoInstall = options.DisableAutoInstall,
            OverrideMinimumInstallVersion = options.OverrideMinimumInstallVersion,
            AllowDowngrade = options.AllowDowngrade,
        });

        lambdaEmulator.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables[Constants.IsAspireHostedEnvVariable] = "true";
            context.EnvironmentVariables["LAMBDA_RUNTIME_API_PORT"] = endpointHttpReference.Property(EndpointProperty.TargetPort);

            if (!options.DisableHttpsEndpoint && endpointHttpsReference != null)
            {
                context.EnvironmentVariables["LAMBDA_WEB_UI_HTTPS_PORT"] = endpointHttpsReference.Property(EndpointProperty.TargetPort);
            }
        }));

        serviceEmulator = lambdaEmulator.Resource;
        builder.Services.TryAddEventingSubscriber<LambdaBeforeStartEventHandler>();

        return lambdaEmulator;
    }

    private static ExecutableResource AddOrGetLambdaServiceEmulatorResource(IDistributedApplicationBuilder builder)
    {
        if (builder.Resources.FirstOrDefault(x => x.TryGetAnnotationsOfType<LambdaEmulatorAnnotation>(out _)) is not ExecutableResource serviceEmulator)
        {
            serviceEmulator = builder.AddAWSLambdaServiceEmulator().Resource;
        }

        return serviceEmulator;
    }

    /// <summary>
    /// This method is adapted from the Aspire WithProjectDefaults method.
    /// https://github.com/dotnet/aspire/blob/157f312e39300912b37a14f59beda217c8195e14/src/Aspire.Hosting/ProjectResourceBuilderExtensions.cs#L287
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    private static IResourceBuilder<LambdaProjectResource> WithOpenTelemetry(this IResourceBuilder<LambdaProjectResource> builder)
    {
        builder.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES", "true");
        builder.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true");
        // .NET SDK has experimental support for retries. Enable with env var.
        // https://github.com/open-telemetry/opentelemetry-dotnet/pull/5495
        // Remove once retry feature in opentelemetry-dotnet is enabled by default.
        builder.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY", "in_memory");

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode && builder.ApplicationBuilder.Environment.IsDevelopment())
        {
            // Disable URL query redaction, e.g. ?myvalue=Redacted
            builder.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION", "true");
            builder.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION", "true");
        }

        builder.WithOtlpExporter();

        return builder;
    }
}
