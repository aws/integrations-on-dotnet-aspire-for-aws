// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.CDKResourceContexts;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting;

public static partial class AWSCDKEnvironmentExtensions
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CDKPublishingGenerator, CDKPublishingGenerator>();
        builder.Services.TryAddSingleton<CDKDeployContext, CDKDeployContext>();
        builder.Services.TryAddSingleton<ITarballContainerImageBuilder, DefaultTarballContainerImageBuilder>();
        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();
        builder.Services.TryAddSingleton<ILambdaDeploymentPackager, LambdaDeploymentPackager>();

        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateExpressServicePublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateServicePublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateServiceWithALBPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ElastiCacheNodeClusterPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ElastiCacheServerlessClusterPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, LambdaFunctionPublishTarget>();
    }

    /// <summary>
    /// Adds the Aspire environment to deploy resources using AWS. The DefaultProvider is used configure the default choices used
    /// when deploying resources. This is generally set to <see cref="DefaultProvider.V1"/>. As AWS evolves and defaults need
    /// to change a new version will be created. Users can then opt-in to when they want to migrated to the new version. 
    /// See <see cref="DefaultProvider"/> for more details.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">The Aspire resource name also used as the CloudFormation stack name.</param>
    /// <param name="defaultProvider">The DefaultProvider is used configure the default choices used when deploying resources.</param>
    /// <returns></returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<Stack>> AddAWSCDKEnvironment(this IDistributedApplicationBuilder builder, [ResourceName] string name, DefaultProvider defaultProvider)
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<Stack>(name, defaultProvider, app => new Stack(app, name));

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    /// <summary>
    /// Adds the Aspire environment to deploy resources using AWS. The DefaultProvider is used configure the default choices used
    /// when deploying resources. This is generally set to <see cref="DefaultProvider.V1"/>. As AWS evolves and defaults need
    /// to change a new version will be created. Users can then opt-in to when they want to migrated to the new version. 
    /// See <see cref="DefaultProvider"/> for more details.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">The Aspire resource name also used as the CloudFormation stack name.</param>
    /// <param name="defaultProvider">The DefaultProvider is used configure the default choices used when deploying resources.</param>
    /// <param name="stackFactory">Func to provide a custom CDK stack with it's own resources. The Aspire provisioned resource will be added to this CDK stack.</param>
    /// <returns></returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<T>> AddAWSCDKEnvironment<T>(this IDistributedApplicationBuilder builder, [ResourceName] string name, DefaultProvider defaultProvider, Func<App, T> stackFactory)
        where T : Stack
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<T>(name, defaultProvider, stackFactory);

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }
}
