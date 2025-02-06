// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Versioning;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

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
    public static IResourceBuilder<LambdaProjectResource> AddAWSLambdaFunction<TLambdaProject>(this IDistributedApplicationBuilder builder, string name, string lambdaHandler) where TLambdaProject : IProjectMetadata, new()
    {
        var metadata = new TLambdaProject();

        var serviceEmulator = AddOrGetLambdaServiceEmulatorResource(builder);

        IResourceBuilder<LambdaProjectResource> resource;
        if (lambdaHandler.Contains("::"))
        {
            // TODO Handle Class Library based Lambda functions.
            throw new NotImplementedException("Currently the Lambda Aspire integration does not support class library based Lambda functions. Class library support will be implemented once a committed change to Aspire has been released allowing the feature to be implemented.");
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
            context.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = apiPath;
            context.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = name;
            context.EnvironmentVariables["_HANDLER"] = lambdaHandler;

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

    private static ExecutableResource AddOrGetLambdaServiceEmulatorResource(IDistributedApplicationBuilder builder)
    {        
        if (builder.Resources.FirstOrDefault(x => x.TryGetAnnotationsOfType<LambdaEmulatorAnnotation>(out _)) is not ExecutableResource serviceEmulator)
        {
            var serviceEmulatorBuilder = builder.AddExecutable($"Lambda-ServiceEmulator",
                                                    "dotnet-lambda-test-tool",
                                                    Environment.CurrentDirectory,
                                                    "--no-launch-window")
                                    .ExcludeFromManifest();

            var annotation = new EndpointAnnotation(
                protocol: ProtocolType.Tcp,
                uriScheme: "http");

            serviceEmulatorBuilder.WithAnnotation(annotation);
            var endpointReference = new EndpointReference(serviceEmulatorBuilder.Resource, annotation);

            serviceEmulatorBuilder.WithAnnotation(new LambdaEmulatorAnnotation(endpointReference));

            serviceEmulatorBuilder.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables[Constants.IsAspireHostedEnvVariable] = "true";
                context.EnvironmentVariables["LAMBDA_RUNTIME_API_PORT"] = endpointReference.Property(EndpointProperty.TargetPort);
            }));

            serviceEmulator = serviceEmulatorBuilder.Resource;
        }

        return serviceEmulator;
    }
}
