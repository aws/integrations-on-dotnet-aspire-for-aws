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
    [AspireExport]
    public static IResourceBuilder<APIGatewayEmulatorResource> AddAWSAPIGatewayEmulator(this IDistributedApplicationBuilder builder, string name, APIGatewayType apiGatewayType, APIGatewayEmulatorOptions? options = null)
    {
        options ??= new APIGatewayEmulatorOptions();

        var apiGatewayEmulator = builder.AddResource(new APIGatewayEmulatorResource(name, apiGatewayType)).ExcludeFromManifest();
        apiGatewayEmulator.WithArgs(context =>
        {
            apiGatewayEmulator.Resource.AddCommandLineArguments(context.Args);
        });

        var annotationHttp = new EndpointAnnotation(
            protocol: ProtocolType.Tcp,
            uriScheme: "http",
            port: options.HttpPort);
        apiGatewayEmulator.WithAnnotation(annotationHttp);
        var endpointHttpReference = new EndpointReference(apiGatewayEmulator.Resource, annotationHttp);

        EndpointReference? endpointHttpsReference = null;

        if (!options.DisableHttpsEndpoint)
        {
            var annotationHttps = new EndpointAnnotation(
                protocol: ProtocolType.Tcp,
                uriScheme: "https",
                port: options.HttpsPort);
            apiGatewayEmulator.WithAnnotation(annotationHttps);
            endpointHttpsReference = new EndpointReference(apiGatewayEmulator.Resource, annotationHttps);

            apiGatewayEmulator.WithUrlForEndpoint("https", u => u.DisplayText = "API Gateway Endpoint");
        }
        else
        {
            apiGatewayEmulator.WithUrlForEndpoint("http", u => u.DisplayText = "API Gateway Endpoint");
        }


        apiGatewayEmulator.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables[Constants.IsAspireHostedEnvVariable] = "true";
            context.EnvironmentVariables["API_GATEWAY_EMULATOR_PORT"] = endpointHttpReference.Property(EndpointProperty.TargetPort);

            if (endpointHttpsReference != null)
            {
                context.EnvironmentVariables["API_GATEWAY_EMULATOR_HTTPS_PORT"] = endpointHttpsReference.Property(EndpointProperty.TargetPort);
            }
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
    [AspireExport("withAPIGatewayLambdaReference")]
    public static IResourceBuilder<APIGatewayEmulatorResource> WithReference(this IResourceBuilder<APIGatewayEmulatorResource> builder, IResourceBuilder<LambdaProjectResource> lambda, Method httpMethod, string path)
    {
        LambdaEmulatorAnnotation? lambdaEmulatorAnnotation = null;
        if (builder.ApplicationBuilder.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out lambdaEmulatorAnnotation)) == null ||
            lambdaEmulatorAnnotation == null)
        {
            return builder;
        }

        builder.WithReference(lambdaEmulatorAnnotation.LambdaRuntimeEndpoint);

        var routes = builder.Resource.Annotations.OfType<ApiGatewayEmulatorRoutesAnnotation>().FirstOrDefault();
        if (routes is null)
        {
            routes = new ApiGatewayEmulatorRoutesAnnotation();
            builder.WithAnnotation(routes);
        }

        var lambdaName = lambda.Resource.Name;
        var endpoint = lambdaEmulatorAnnotation.LambdaRuntimeEndpoint;
        routes.LambdaRoutes.Add(new ApiGatewayEmulatorLambdaRoute
        {
            LambdaResourceName = lambdaName,
            HttpMethod = httpMethod,
            Path = path,
            Endpoint = () => endpoint.Url
        });

        if (!routes.EnvironmentCallbackRegistered)
        {
            routes.EnvironmentCallbackRegistered = true;
            builder.WithEnvironment(context =>
            {
                foreach (var group in routes.LambdaRoutes.GroupBy(r => r.LambdaResourceName, StringComparer.Ordinal))
                {
                    var envName = "APIGATEWAY_EMULATOR_ROUTE_CONFIG_" + group.Key;
                    var configs = group.Select(r => new RouteConfig(
                        r.LambdaResourceName,
                        r.Endpoint(),
                        r.HttpMethod,
                        r.Path)).ToList();
                    context.EnvironmentVariables[envName] = JsonSerializer.Serialize(configs);
                }
            });
        }

        return builder;
    }

    /// <summary>
    /// Add a reference for a hosted project to be proxied by the API Gateway emulator via HTTP integration.
    /// The project's HTTP endpoint is used as the backend; the emulator proxies requests to it.
    /// </summary>
    /// <param name="builder">The API Gateway emulator resource builder.</param>
    /// <param name="project">The project resource (must have an "http" endpoint).</param>
    /// <param name="httpMethod">The HTTP method the route should match.</param>
    /// <param name="path">The resource path (e.g. "/api/users" or "/api/{proxy+}").</param>
    /// <returns>The API Gateway emulator resource builder.</returns>
    [AspireExport("withAPIGatewayHttpReference")]
    public static IResourceBuilder<APIGatewayEmulatorResource> WithReference(this IResourceBuilder<APIGatewayEmulatorResource> builder, IResourceBuilder<ProjectResource> project, Method httpMethod, string path)
    {
        if (builder is IResourceBuilder<IResourceWithWaitSupport> waitSupport)
        {
            waitSupport.WaitFor(project);
        }

        var endpointRef = project.GetEndpoint("http");
        var resourceName = project.Resource.Name;
        var methodStr = httpMethod.ToString();
        var integration = ApiGatewayIntegrationType.Http.ToString();
        var configExpr = ReferenceExpression.Create($"{{\"LambdaResourceName\":\"{resourceName}\",\"Endpoint\":\"{endpointRef}\",\"HttpMethod\":\"{methodStr}\",\"Path\":\"{path}\",\"IntegrationType\":\"{integration}\"}}");
        // Unique suffix: the test tool loads every APIGATEWAY_EMULATOR_ROUTE_CONFIG_* value.
        // Reusing the resource name alone would keep only the last WithReference path.
        var envName = "APIGATEWAY_EMULATOR_ROUTE_CONFIG_" + resourceName + "_" + methodStr + "_" + SanitizePathForEnvName(path);
        builder.WithEnvironment(envName, configExpr);

        return builder;
    }

    static string SanitizePathForEnvName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "root";
        var chars = path.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "root" : sanitized;
    }
}
