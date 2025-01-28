// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
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
                context.EnvironmentVariables["LAMBDA_RUNTIME_API_PORT"] = endpointReference.Property(EndpointProperty.TargetPort);
            }));

            serviceEmulator = serviceEmulatorBuilder.Resource;
        }

        return serviceEmulator;
    }
}
