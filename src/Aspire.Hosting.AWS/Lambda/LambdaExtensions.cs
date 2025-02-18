// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Hosting;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Aspire.Hosting.AWS.Utils;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

internal sealed class LambdaProjectMetadata(string projectPath) : IProjectMetadata
{
    public string ProjectPath { get; } = projectPath;
}

/// <summary>
/// Extension methods for adding Lambda functions as Aspire resources.
/// </summary>
[RequiresPreviewFeatures(Constants.LambdaPreviewMessage)]
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
    /// <exception cref="NotImplementedException"></exception>
    public static IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunction<TLambdaProject>(this IDistributedApplicationBuilder builder, string name, string lambdaHandler, LambdaFunctionOptions? options = null) where TLambdaProject : IProjectMetadata, new()
    {
        options ??= new LambdaFunctionOptions();
        var metadata = new TLambdaProject();

        var serviceEmulator = AddOrGetLambdaServiceEmulatorResource(builder);
        IResourceBuilder<LambdaProjectResource> resource;
        if (lambdaHandler.Contains("::"))
        {
            if(AspireUtilities.IsRunningInDebugger)
            {
                // TODO: Once 9.1 comes out LaunchProfileAnnotation will be public and we can remove the reflection and directly instantiate it.
                var launchProfileAnnotationsType = typeof(IDistributedApplicationBuilder).Assembly.GetTypes().FirstOrDefault(x => string.Equals(x.FullName, "Aspire.Hosting.ApplicationModel.LaunchProfileAnnotation"));
                var constructor = launchProfileAnnotationsType!.GetConstructors()[0];
                var instance = constructor.Invoke(new object[] { $"{Constants.LaunchSettingsNodePrefix}{name}" }) as IResourceAnnotation;

                var project = new LambdaProjectResource(name);
                resource = builder.AddResource(project)
                    .WithAnnotation(instance!)
                    .WithAnnotation(new TLambdaProject());
            }
            else
            {
                var project = new LambdaProjectResource(name);
                resource = builder.AddResource(project)
                    // .WithAnnotation(new LambdaProjectMetadata(projectPath))
                    .WithAnnotation(new TLambdaProject());
                var projectMetadata = project.Annotations.OfType<IProjectMetadata>().First();
                project.Annotations.Remove(projectMetadata);
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                // Define the .csproj content.
                var projectContent = $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Amazon.Lambda.RuntimeSupport"" Version=""1.12.2"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""{projectMetadata.ProjectPath}"" />
  </ItemGroup>
</Project>
";
                var projectName = $"Wrapper{Path.GetFileName(projectMetadata.ProjectPath)}";
                var projectPath = Path.Combine(tempPath, projectName);
                File.WriteAllText(projectPath, projectContent);
                
                string programContent = $@"
using Amazon.Lambda.RuntimeSupport;

RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(""{lambdaHandler}"");
await runtimeSupportInitializer.RunLambdaBootstrap();
";
                var programPath = Path.Combine(tempPath, "Program.cs");
                File.WriteAllText(programPath, programContent);

                RunProcess("dotnet", $"build {projectPath}", Directory.GetParent(projectMetadata.ProjectPath)!.FullName);
                // RunProcess("dotnet", $"build -c Release {projectPath}");
                
                resource = resource
                    .WithAnnotation(new LambdaProjectMetadata(projectPath));
            }
        }
        else
        {
            var project = new LambdaProjectResource(name);
            resource = builder.AddResource(project)
                            .WithAnnotation(new TLambdaProject());
        }

        resource.WithOpenTelemetry();

        resource.WithEnvironment(context =>
        {
            var serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("http");

            // Add the Lambda function resource on the path so the emulator can distingish request
            // for each Lambda function.
            var apiPath = $"{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/{name}";
            context.EnvironmentVariables["AWS_EXECUTION_ENV"] = $"aspire.hosting.aws#{SdkUtilities.GetAssemblyVersion()}";
            context.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = apiPath;
            context.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = name;
            context.EnvironmentVariables["_HANDLER"] = lambdaHandler;

            context.EnvironmentVariables["AWS_LAMBDA_LOG_FORMAT"] = options.LogFormat.Value;
            context.EnvironmentVariables["AWS_LAMBDA_LOG_LEVEL"] = options.ApplicationLogLevel.Value;

            var lambdaEmulatorEndpoint = $"http://{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/?function={Uri.EscapeDataString(name)}";
            
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
    /// Add the Lambda service emulator resource. The <see cref="AddAWSLambdaFunction"/> method will automatically add the Lambda service emulator if it hasn't
    /// already been added. This method only needs to be called if the emulator needs to be customized with the <see cref="LambdaEmulatorOptions"/>. If
    /// this method is called it must be called only once and before any <see cref="AddAWSLambdaFunction"/> calls.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options">The options to configure the emulator with.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if the Lambda service emulator has already been added.</exception>
    public static IResourceBuilder<LambdaEmulatorResource> AddAWSLambdaServiceEmulator(this IDistributedApplicationBuilder builder, LambdaEmulatorOptions? options = null)
    {
        if (builder.Resources.FirstOrDefault(x => x.TryGetAnnotationsOfType<LambdaEmulatorAnnotation>(out _)) is ExecutableResource serviceEmulator)
        {
            throw new InvalidOperationException("A Lambda service emulator has already been added. The AddAWSLambdaFunction will add the emulator " +
                "if it hasn't already been added. This method must be called before AddAWSLambdaFunction if the Lambda service emulator needs to be customized.");
        }

        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();

        var lambdaEmulator = builder.AddResource(new LambdaEmulatorResource("LambdaServiceEmulator")).ExcludeFromManifest();
        lambdaEmulator.WithArgs(context =>
        {
            lambdaEmulator.Resource.AddCommandLineArguments(context.Args);
        });

        var annotation = new EndpointAnnotation(
            protocol: ProtocolType.Tcp,
            uriScheme: "http");

        lambdaEmulator.WithAnnotation(annotation);
        var endpointReference = new EndpointReference(lambdaEmulator.Resource, annotation);

        lambdaEmulator.WithAnnotation(new LambdaEmulatorAnnotation(endpointReference)
        {
            DisableAutoInstall = options?.DisableAutoInstall ?? false,
            OverrideMinimumInstallVersion = options?.OverrideMinimumInstallVersion,
            AllowDowngrade = options?.AllowDowngrade ?? false,
        });

        lambdaEmulator.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables[Constants.IsAspireHostedEnvVariable] = "true";
            context.EnvironmentVariables["LAMBDA_RUNTIME_API_PORT"] = endpointReference.Property(EndpointProperty.TargetPort);
        }));

        serviceEmulator = lambdaEmulator.Resource;
        builder.Services.TryAddLifecycleHook<LambdaLifecycleHook>();

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
    
    /// <summary>
    /// Utility method for running a command on the commandline. It returns backs the exit code and anything written to stdout or stderr.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="path"></param>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static int RunProcess(string path, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = path,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var output = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.Append(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.Append(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(int.MaxValue);

        return process.ExitCode;
    }
}
