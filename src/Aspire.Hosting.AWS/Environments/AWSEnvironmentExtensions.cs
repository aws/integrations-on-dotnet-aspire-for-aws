// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting;

public static class AWSEnvironmentExtensions
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CDKPublishingContext, CDKPublishingContext>();
        builder.Services.TryAddSingleton<CDKDeployContext, CDKDeployContext>();
        builder.Services.TryAddSingleton<ITarballContainerImageBuilder, DefaultTarballContainerImageBuilder>();
        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();
        builder.Services.TryAddSingleton<ILambdaDeploymentPackager, LambdaDeploymentPackager>();
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<Stack>> AddAWSCDKEnvironment(this IDistributedApplicationBuilder builder, [ResourceName] string name, DeploymentComputeService preferredComputeService, DefaultProvider defaultProvider)
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<Stack>(name, preferredComputeService, defaultProvider, app => new Stack(app, name));

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<T>> AddAWSCDKEnvironment<T>(this IDistributedApplicationBuilder builder, [ResourceName] string name, DeploymentComputeService preferredComputeService, DefaultProvider defaultProvider, Func<App, T> stackFactory)
        where T : Stack
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<T>(name, preferredComputeService, defaultProvider, stackFactory);

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<LambdaProjectResource> PublishAsLambdaFunction(this IResourceBuilder<LambdaProjectResource> builder, PublishCDKLambdaConfig config)
    {
        var annotation = new PublishCDKLambdaAnnotation { Config = config };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateServiceWithALB(this IResourceBuilder<ProjectResource> builder, PublishCDKECSFargateWithALBConfig config)
    {
        var annotation = new PublishCDKECSFargateWithALBAnnotation { Config = config };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateServiceExpress(this IResourceBuilder<ProjectResource> builder, PublishCDKECSFargateExpressConfig config)
    {
        var annotation = new PublishCDKECSFargateExpressAnnotation { Config = config };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateService(this IResourceBuilder<ProjectResource> builder, PublishCDKECSFargateConfig config)
    {
        var annotation = new PublishCDKECSFargateAnnotation { Config = config };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<RedisResource> PublishAsElasticCacheCluster(this IResourceBuilder<RedisResource> builder, PublishCDKElastiCacheRedisConfig config)
    {
        var annotation = new PublishCDKElasticCacheRedisAnnotation { Config = config };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
}
