// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Lambda;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text.Json;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

/// <summary>
/// Extension methods adding and interacting with the API Gateway emulator.
/// </summary>
[RequiresPreviewFeatures(Constants.LambdaPreviewMessage)]
public static class APIGatewayExtensions
{
    /// <summary>
    /// Adds an API Gateway emulator resource to the Aspire application. Lambda function resources
    /// should be added to this resource using the WithReference method.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">Aspire resource name</param>
    /// <param name="apiGatewayType">The type of API Gateway API. For example Rest, HttpV1 or HttpV2</param>
    /// <param name="options">The options to configure the emulator with.</param>
    /// <returns></returns>
    public static IResourceBuilder<APIGatewayEmulatorResource> AddAWSAPIGatewayEmulator(this IDistributedApplicationBuilder builder, string name, APIGatewayType apiGatewayType, APIGatewayEmulatorOptions? options = null)
    {
        var apiGatewayEmulator = builder.AddResource(new APIGatewayEmulatorResource(name, apiGatewayType)).ExcludeFromManifest();
        apiGatewayEmulator.WithArgs(context =>
        {
            apiGatewayEmulator.Resource.AddCommandLineArguments(context.Args);
        });

        var annotation = new EndpointAnnotation(
            protocol: ProtocolType.Tcp,
            uriScheme: "http",
            port: options?.Port);

        apiGatewayEmulator.WithAnnotation(annotation);
        var endpointReference = new EndpointReference(apiGatewayEmulator.Resource, annotation);

        apiGatewayEmulator.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables[Constants.IsAspireHostedEnvVariable] = "true";
            context.EnvironmentVariables["API_GATEWAY_EMULATOR_PORT"] = endpointReference.Property(EndpointProperty.TargetPort);
        }));

        apiGatewayEmulator.WithAnnotation(new APIGatewayEmulatorAnnotation(apiGatewayType));

        return apiGatewayEmulator;
    }

    /// <summary>
    /// Add a reference for a Lambda function to be called by the API Gateway emulator for a particular HTTP method and resource path. The resource path can use 
    /// variables like "/customer/{id}" or wild card paths like "/admin/{proxy+}".
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="lambda">The Lambda resource to add to the API Gateway emulator</param>
    /// <param name="httpMethod">The HTTP method the Lambda function should be called for.</param>
    /// <param name="path">The resource path the Lambda function should be called for.</param>
    /// <returns></returns>
    public static IResourceBuilder<APIGatewayEmulatorResource> WithReference(this IResourceBuilder<APIGatewayEmulatorResource> builder, IResourceBuilder<LambdaProjectResource> lambda, Method httpMethod, string path)
    {
        LambdaEmulatorAnnotation? lambdaEmulatorAnnotation = null;
        if (builder.ApplicationBuilder.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out lambdaEmulatorAnnotation)) == null ||
            lambdaEmulatorAnnotation == null)
        {
            return builder;
        }

        builder.WithReference(lambdaEmulatorAnnotation.Endpoint);

        builder.WithEnvironment(context =>
        {
            var envName = "APIGATEWAY_EMULATOR_ROUTE_CONFIG_" + lambda.Resource.Name;
            var config = new RouteConfig(lambda.Resource.Name, lambdaEmulatorAnnotation.Endpoint.Url, httpMethod, path);
            var configJson = JsonSerializer.Serialize(config);
            context.EnvironmentVariables[envName] = configJson;
        });

        return builder;
    }
}
