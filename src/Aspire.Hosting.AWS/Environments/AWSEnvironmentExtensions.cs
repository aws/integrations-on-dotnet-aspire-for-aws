using Aspire.Hosting.ApplicationModel;
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Stack = Amazon.CDK.Stack;
using App = Amazon.CDK.App;
using Aspire.Hosting.AWS.Lambda;
using Amazon.CDK.AWS.Lambda;

namespace Aspire.Hosting;

public static class AWSEnvironmentExtensions
{
    private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();
        builder.Services.TryAddSingleton<ILambdaDeploymentPackager, LambdaDeploymentPackager>();
//        builder.Services.TryAddLifecycleHook<CDKInfrastructureLifecycleHook>();
    }

    public static IResourceBuilder<AWSCDKEnvironmentResource<Stack>> AddAWSCDKEnvironment(this IDistributedApplicationBuilder builder, string name)
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<Stack>(name, app => new Stack(app, name));

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    public static IResourceBuilder<AWSCDKEnvironmentResource<T>> AddAWSCDKEnvironment<T>(this IDistributedApplicationBuilder builder, string name, Func<App, T> stackFactory)
        where T : Stack
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<T>(name, stackFactory);

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    public static IResourceBuilder<LambdaProjectResource> WithPublishingCDKPropsCallback(this IResourceBuilder<LambdaProjectResource> builder, Action<FunctionProps> callback)
    {
        var annotations = new PublishingCDKConfigureCallbackAnnotation { LambdaFunctionPropsCallback = callback };
        builder.Resource.Annotations.Add(annotations);

        return builder;
    }

    public static IResourceBuilder<LambdaProjectResource> WithPublishingCDKConstructCallback(this IResourceBuilder<LambdaProjectResource> builder, Action<Function> callback)
    {
        var annotations = new PublishingCDKConfigureCallbackAnnotation { LambdaFunctionConstructCallback = callback };
        builder.Resource.Annotations.Add(annotations);

        return builder;
    }
}
