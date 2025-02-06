﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Lifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
}
