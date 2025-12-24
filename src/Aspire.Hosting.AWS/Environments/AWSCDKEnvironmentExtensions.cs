// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.CDKPublishTargets;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.Services;
using Aspire.Hosting.AWS.Lambda;
using App = Amazon.CDK.App;
using Stack = Amazon.CDK.Stack;

namespace Aspire.Hosting;

public static partial class AWSCDKEnvironmentExtensions
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CDKPublishingStep, CDKPublishingStep>();
        builder.Services.TryAddSingleton<CDKDeployStep, CDKDeployStep>();
        builder.Services.TryAddSingleton<ITarballContainerImageBuilder, DefaultTarballContainerImageBuilder>();
        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();
        builder.Services.TryAddSingleton<ILambdaDeploymentPackager, DefaultLambdaDeploymentPackager>();

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
    
    /// <summary>
    /// Deploy to AWS Elastic Container Service using the <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html">Express Mode</a>.
    /// Express mode deploys as an ECS service and a shared Application Load Balancer (ALB) across your Express mode services to route traffic to the service. 
    /// An HTTPS endpoint will be provisioned by default and a TargetGroup rule added to the ALB for the provisioned host name.
    /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.CfnExpressGatewayService.html">CfnExpressGatewayService</a> construct is used to create the ECS Express Gateway service.
    /// </summary>
    /// <remarks>
    /// Port 8080 is assumed to be the container port the web application listens on. This can be customized by adding a callback on the config's PropsCfnExpressGatewayServicePropsCallback property.
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateExpressService(this IResourceBuilder<ProjectResource> builder, PublishECSFargateExpressServiceConfig? config = null)
    {
        var annotation = new PublishECSFargateServiceExpressAnnotation { Config = config ?? new PublishECSFargateExpressServiceConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
    
    /// <summary>
    /// Deploy to as a service to the AWS Elastic Container Service (ECS). An ECS service is a continuously running set of tasks running the console application as a container.
    /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.FargateService.html">FargateService</a> construct is used to create the ECS service.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateService(this IResourceBuilder<ProjectResource> builder, PublishECSFargateServiceConfig? config = null)
    {
        var annotation = new PublishECSFargateServiceAnnotation { Config = config ?? new PublishECSFargateServiceConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
    
    /// <summary>
    /// Deploy to AWS ECS Fargate Service with Application Load Balancer. This uses the CDK 
    /// <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs_patterns.ApplicationLoadBalancedFargateService.html">
    /// ApplicationLoadBalancedFargateService</a> construct. This construct will create an ECS Fargate service fronted by an 
    /// Application Load Balancer (ALB) to distribute incoming traffic across multiple instances of the web application.
    /// By default an HTTP endpoint will be provisioned.
    /// </summary>
    /// <remarks>
    /// Port 8080 is assumed to be the container port the web application listens on. This can be customized by adding a callback on the config's PropsApplicationLoadBalancedTaskImageOptionsCallback property.
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateServiceWithALB(this IResourceBuilder<ProjectResource> builder, PublishECSFargateServiceWithALBConfig? config = null)
    {
        var annotation = new PublishCDKECSFargateServiceWithALBAnnotation { Config = config ?? new PublishECSFargateServiceWithALBConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
    
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<RedisResource> PublishAsElasticCacheNodeCluster(this IResourceBuilder<RedisResource> builder, PublishElastiCacheNodeClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheNodeClusterAnnotation { Config = config ?? new PublishElastiCacheNodeClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
    
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<RedisResource> PublishAsElasticCacheServerlessCluster(this IResourceBuilder<RedisResource> builder, PublishElastiCacheServerlessClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheServerlessClusterAnnotation { Config = config ?? new PublishElastiCacheServerlessClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }
    
    /// <summary>
    /// Deploy project as to AWS Lambda as a function.
    /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_lambda.Function.html">Function</a> construct is used to create the Lambda function.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config">Configuration for attaching callbacks to configure the CDK construct's props and associate the created CDK construct to other CDK constructs.</param>
    /// <returns></returns>
    [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<LambdaProjectResource> PublishAsLambdaFunction(this IResourceBuilder<LambdaProjectResource> builder, PublishLambdaFunctionConfig? config = null)
    {
        var annotation = new PublishLambdaFunctionAnnotation { Config = config ?? new PublishLambdaFunctionConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }    
}
