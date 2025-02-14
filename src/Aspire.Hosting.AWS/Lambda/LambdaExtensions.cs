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
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    public static int RunProcess(string path, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
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
    
    private static string CreateWrapperProject(string classLibraryPath, string lambdaHandler)
    {
        var tempFolder = Directory.CreateTempSubdirectory();
        // var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
        if (!Directory.Exists(tempFolder.FullName))
            Directory.CreateDirectory(tempFolder.FullName);
        var projectFileName = Path.GetFileName(classLibraryPath);
        var wrapperProjectFilePath = Path.Combine(tempFolder.FullName, $"Wrapper{projectFileName}");
        var projectFileContent = @$"
<Project Sdk=""Microsoft.NET.Sdk"">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include=""Amazon.Lambda.RuntimeSupport"" Version=""1.12.2"" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include=""{classLibraryPath}"" />
    </ItemGroup>

</Project>
";
        var entryPointFilePath = Path.Combine(tempFolder.FullName, "Program.cs");
        var entryPointContent = @$"
using Amazon.Lambda.RuntimeSupport;

RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(""{lambdaHandler}"");
await runtimeSupportInitializer.RunLambdaBootstrap();
";
        File.WriteAllText(wrapperProjectFilePath, projectFileContent);
        File.WriteAllText(entryPointFilePath, entryPointContent);

        RunProcess("dotnet", $"build {wrapperProjectFilePath}");
        RunProcess("dotnet", $"build -c Release {wrapperProjectFilePath}");
        return wrapperProjectFilePath;
    }
    
    /// <summary>
    /// Add a Lambda function as an Aspire resource.
    /// </summary>
    /// <typeparam name="TLambdaProject"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name">Aspire resource name</param>
    /// <param name="lambdaHandler">Lambda function handler</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunction<TLambdaProject>(this IDistributedApplicationBuilder builder, string name, string lambdaHandler) where TLambdaProject : IProjectMetadata, new()
    {
        var metadata = new TLambdaProject();

        var serviceEmulator = AddOrGetLambdaServiceEmulatorResource(builder);
        IResourceBuilder<LambdaProjectResource> resource;
        if (lambdaHandler.Contains("::"))
        {
            var project = new LambdaProjectResource(name);
            resource = builder.AddResource(project)
                // .WithAnnotation(new LambdaProjectMetadata("/Users/asmarp/CodeBase/integrations-on-dotnet-aspire-for-aws/playground/Lambda/MultiplyLambdaFunctionLibrary/MultiplyLambdaFunctionLibrary.csproj"))
                // .WithAnnotation(new DefaultLaunchProfileAnnotation($"Aspire_{name}"))
                .WithAnnotation(new TLambdaProject());
            var projectMetadata = resource.Resource.Annotations.OfType<IProjectMetadata>().First();
            var wrapperProjectPath = CreateWrapperProject(projectMetadata.ProjectPath, lambdaHandler);
            resource.Resource.Annotations.Remove(projectMetadata);
            // resource = resource.WithAnnotation(new LambdaProjectMetadata("/Users/asmarp/CodeBase/integrations-on-dotnet-aspire-for-aws/playground/Lambda/MultiplyLambdaFunctionWrapper/MultiplyLambdaFunctionWrapper.csproj"));
            resource = resource.WithAnnotation(new LambdaProjectMetadata(wrapperProjectPath));
            // var inter = builder.AddProject("test", "/Users/asmarp/CodeBase/integrations-on-dotnet-aspire-for-aws/playground/Lambda/MultiplyLambdaFunctionLibrary/MultiplyLambdaFunctionLibrary.csproj");
            // inter.WithEnvironment(context =>
            // {
            //     var serviceEmulatorEndpoint = serviceEmulator.GetEndpoint("http");
            //
            //     // Add the Lambda function resource on the path so the emulator can distingish request
            //     // for each Lambda function.
            //     var apiPath = $"{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/{name}";
            //     context.EnvironmentVariables["AWS_EXECUTION_ENV"] = $"aspire.hosting.aws#{SdkUtilities.GetAssemblyVersion()}";
            //     context.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = apiPath;
            //     context.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = name;
            //     context.EnvironmentVariables["_HANDLER"] = lambdaHandler;
            //     
            //     var lambdaEmulatorEndpoint = $"http://{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/?function={Uri.EscapeDataString(name)}";
            //
            //     resource.WithAnnotation(new ResourceCommandAnnotation(
            //         name: "LambdaEmulator", 
            //         displayName: "Lambda Service Emulator", 
            //         updateState: context =>
            //         {
            //             if (string.Equals(context.ResourceSnapshot.State?.Text, KnownResourceStates.Running))
            //             {
            //                 return ResourceCommandState.Enabled;
            //             }
            //             return ResourceCommandState.Disabled;
            //         },
            //         executeCommand: context =>
            //         {
            //             var startInfo = new ProcessStartInfo
            //             {
            //                 UseShellExecute = true,
            //                 FileName = lambdaEmulatorEndpoint
            //             };
            //             Process.Start(startInfo);
            //
            //             return Task.FromResult(CommandResults.Success());
            //         },
            //         displayDescription: "Open the Lambda service emulator configured for this Lambda function",
            //         parameter: null,
            //         confirmationMessage: null,
            //         iconName: "Bug",
            //         iconVariant: IconVariant.Filled,
            //         isHighlighted: true)
            //     );
            // });
        }
        else
        {
            var project = new LambdaProjectResource(name);
            resource = builder.AddResource(project)
                            .WithAnnotation(new TLambdaProject());
        }

        resource.WithOpenTelemetry();

        resource.WithAnnotation(new LambdaFunctionAnnotation(lambdaHandler));

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

            var lambdaEmulatorEndpoint = $"http://{serviceEmulatorEndpoint.Host}:{serviceEmulatorEndpoint.Port}/?function={Uri.EscapeDataString(name)}";
            var processCommandService = context.ExecutionContext.ServiceProvider.GetRequiredService<IProcessCommandService>();
            
            if (lambdaHandler.Contains("::"))
            {
                 // var projectMetadata = resource.Resource.Annotations
                 //     .OfType<IProjectMetadata>()
                 //     .First();
//                 var lambdaEmulator = serviceEmulator.Annotations.OfType<LambdaEmulatorAnnotation>().First();
                 // ProjectUtilities.UpdateLaunchSettingsEndpoint($"Aspire_{name}", apiPath, projectMetadata.ProjectPath);
//                 string classLibraryDllPath;
//                 try
//                 {
//                     classLibraryDllPath = LocateBuiltDll(projectMetadata.ProjectPath);
//                     Console.WriteLine("Class library built at: " + classLibraryDllPath);
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine("Error locating DLL: " + ex.Message);
//                     return;
//                 }
//                 
//                 string code = @"
// using System;
// using Amazon.Lambda.Core;
// using Amazon.Lambda.RuntimeSupport;
// using Amazon.Lambda.Serialization.SystemTextJson;
// using MultiplyLambdaFunctionLibrary;
// using System.IO;
// using System.Threading.Tasks;
//
// namespace DynamicWrapper
// {
//     public class Wrapper
//     {
//         public static async Task Main(string[] args)
//         {
//             await LambdaBootstrapBuilder.Create(
//         (Stream inputStream, ILambdaContext context) =>
//         {
//             using var reader = new StreamReader(inputStream);
//             string input = reader.ReadToEnd();
//                 
//             // Create an instance of your function class and call the handler.
//             var function = new Function();
//             return function.FunctionHandler(input, context);
//         },
//         new DefaultLambdaJsonSerializer())
//     .Build()
//     .RunAsync();
//         }
//     }
// }
// ";
//
//                 // Instantiate our dynamic compiler.
//                 Assembly? assembly = null;
//
//                 try
//                 {
//                     // Compile the wrapper code in memory.
//                     assembly = CompileWrapper(code, classLibraryDllPath, lambdaEmulator.RuntimeSupportPath!);
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine("Compilation error: " + ex.Message);
//                     return;
//                 }
//
//                 // Retrieve the entry point (Main method) from the compiled assembly.
//                 var entryPoint = assembly.EntryPoint;
//                 if (entryPoint == null)
//                 {
//                     Console.WriteLine("No entry point (Main method) found in the compiled assembly.");
//                     return;
//                 }
//
//                 // Prepare parameters for the Main method.
//                 // If Main has parameters, we provide an empty string array; otherwise, we pass null.
//                 var parameters = entryPoint.GetParameters().Length > 0 ? new object[] { new string[0] } : null;
//             
//                 // Invoke the entry point to run the wrapper code.
//                 entryPoint.Invoke(null, parameters);

            }
            
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
        
        return resource;
    }
    
    public static string LocateBuiltDll(string projectPath)
    {
        // Assumes the project is built in "bin/Debug/{targetFramework}".
        // You may adjust this if your configuration or target framework is different.
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

        // For this example, we assume target framework is net6.0.
        var dllPath = Path.Combine(projectDir, "bin", "Debug", "net8.0", $"{projectName}.dll");

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Built DLL not found", dllPath);
        }

        return dllPath;
    }
    
    /// <summary>
        /// Compiles the provided source code in memory and returns the resulting assembly.
        /// </summary>
        /// <param name="sourceCode">The C# source code to compile.</param>
        /// <returns>The compiled Assembly.</returns>
        public static Assembly CompileWrapper(string sourceCode, string classLibraryDllPath, string runtimeSupportPath)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var assemblyName = Path.GetRandomFileName();

            // Gather references from currently loaded assemblies.
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

            // Add a reference to your built class library.
            references.Add(MetadataReference.CreateFromFile(classLibraryDllPath));

            // Add a reference to Amazon.Lambda.RuntimeSupport.
            try
            {
                references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeSupportPath, "..", "Amazon.Lambda.Core.dll")));
                references.Add(MetadataReference.CreateFromFile(Path.Combine(classLibraryDllPath, "..", "Amazon.Lambda.Serialization.SystemTextJson.dll")));
                references.Add(MetadataReference.CreateFromFile(runtimeSupportPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not load Amazon.Lambda.RuntimeSupport: " + ex.Message);
                // Optionally, if you know the file path, you could add it manually:
                // references.Add(MetadataReference.CreateFromFile(@"C:\Path\To\Amazon.Lambda.RuntimeSupport.dll"));
            }

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            );

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    var errors = string.Join(Environment.NewLine,
                        result.Diagnostics
                              .Where(d => d.Severity == DiagnosticSeverity.Error)
                              .Select(d => d.ToString()));
                    throw new Exception($"Compilation failed: {Environment.NewLine}{errors}");
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
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
        if (builder.Resources.FirstOrDefault(x => x.TryGetAnnotationsOfType<LambdaEmulatorAnnotation>(out var emulatorAnnotations)) is not ExecutableResource serviceEmulator)
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
