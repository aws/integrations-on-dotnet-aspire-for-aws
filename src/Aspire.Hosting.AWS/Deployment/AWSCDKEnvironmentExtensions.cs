// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using App = Amazon.CDK.App;
using Stack = Amazon.CDK.Stack;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Aspire.Hosting;
#pragma warning restore IDE0130

public static class AWSCDKEnvironmentExtensions
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IAWSEnvironmentService, DefaultAWSEnvironmentService>();
        builder.Services.TryAddSingleton<CDKPublishingStep, CDKPublishingStep>();
        builder.Services.TryAddSingleton<CDKDeployStep, CDKDeployStep>();
        builder.Services.TryAddSingleton<ITarballContainerImageBuilder, DefaultTarballContainerImageBuilder>();
        builder.Services.TryAddSingleton<IProcessCommandService, ProcessCommandService>();
        builder.Services.TryAddSingleton<ILambdaDeploymentPackager, DefaultLambdaDeploymentPackager>();

        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateExpressServicePublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateServicePublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ECSFargateServiceWithALBPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ElastiCacheProvisionClusterPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, ElastiCacheServerlessClusterPublishTarget>();
        builder.Services.AddTransient<IAWSPublishTarget, LambdaFunctionPublishTarget>();
    }

    /// <summary>
    /// Adds the AWS CDK Aspire environment used to transform the resources defined in the Aspire AppHost into AWS CDK constructs during the publishing
    /// and deploy phases.
    /// <para>
    /// Node.js 22.x and AWS CDK must be installed on the machine running the deployment. For information installing AWS CDK after Node.js is installed <see href="https://docs.aws.amazon.com/cdk/v2/guide/getting-started.html"/>.
    /// Once AWS CDK is installed ensure the CDK bootstrap has run on the target account and region. For bootstrapping instructions see <see href="https://docs.aws.amazon.com/cdk/v2/guide/bootstrapping.html"/>.
    /// </para>
    /// <para>
    /// The CDKDefaultsProviderFactory is used configure the factory to create the CDKDefaultsProvider. The default provider is used to provide 
    /// default values like memory sizes, cpu limits, expected ports, etc. for various AWS CDK constructs created as part of the Aspire deployment.
    /// It also provides default CDK constructs like VPCs, ECS Clusters, Security Groups, etc. that will be shared across multiple resources.
    /// </para>
    /// <para>
    /// To define your own default constructs like a VPC use the AddAWSCDKEnvironment overload that takes a factory method to create a custom CDK Stack.
    /// In the CDK stack class you can define your own default resources and apply the AWS Aspire default attribute on the resource to identify the resource
    /// as the default. The following example shows how to define a stack that uses an account's default VPC.
    /// </para>
    /// <code>
    /// public class DeploymentStack : Stack
    /// {
    ///     public DeploymentStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    ///     {
    ///         DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
    ///         {
    ///             IsDefault = true
    ///         });
    ///     }
    ///
    ///     [DefaultVpc]
    ///     public IVpc DefaultVpc { get; private set; }
    /// }
    /// </code>
    /// <para>
    /// The Aspire.Hosting.AWS.Deployment namespace attributes using the naming pattern "Default&lt;resource&gt;Attribute" to identify default resources in the CDK stack.
    /// For example <see cref="Aspire.Hosting.AWS.Deployment.DefaultVpcAttribute"/> is used to identify the default VPC resource in the CDK stack.
    /// </para>
    /// <para>
    /// <see cref="CDKDefaultsProviderFactory.Preview_V1"/> is the latest version of the defaults provider provided by the library. As new versions
    /// are created there can be breaking changes that will be documented for each version. This breaking changes might cause AWS resources to be
    /// removed and recreated during deployment. Users can choose when to upgrade to a new version of the defaults provider by changing the factory.
    /// Users can also extend from the existing versions to create their own custom defaults provider and override any of the default settings.
    /// See <see cref="CDKDefaultsProviderFactory"/> for more details.
    /// </para>
    /// </summary>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="name">The Aspire resource name also used as the CloudFormation stack name.</param>
    /// <param name="cdkDefaultsProviderFactory">The DefaultProvider is used configure the default choices used when deploying resources.</param>
    /// <param name="environmentResourceConfig">Additional optional configuration for the environment. For example specifying the AWS SDK configuration.</param>
    /// <param name="stackName">Optional CloudFormation stack name. If not provided the Aspire resource name will be used as the stack name.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<Stack>> AddAWSCDKEnvironment(this IDistributedApplicationBuilder builder, [ResourceName] string name, CDKDefaultsProviderFactory cdkDefaultsProviderFactory, AWSCDKEnvironmentResourceConfig? environmentResourceConfig = null, string? stackName = null)
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<Stack>(name, builder.ExecutionContext.IsPublishMode, cdkDefaultsProviderFactory, (app, props) => new Stack(app, stackName ?? name, props), environmentResourceConfig);

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    /// <summary>
    /// Adds the AWS CDK Aspire environment used to transform the resources defined in the Aspire AppHost into AWS CDK constructs during the publishing
    /// and deploy phases.
    /// <para>
    /// Node.js 22.x and AWS CDK must be installed on the machine running the deployment. For information installing AWS CDK after Node.js is installed <see href="https://docs.aws.amazon.com/cdk/v2/guide/getting-started.html"/>.
    /// Once AWS CDK is installed ensure the CDK bootstrap has run on the target account and region. For bootstrapping instructions see <see href="https://docs.aws.amazon.com/cdk/v2/guide/bootstrapping.html"/>.
    /// </para>
    /// <para>
    /// The CDKDefaultsProviderFactory is used configure the factory to create the CDKDefaultsProvider. The default provider is used to provide 
    /// default values like memory sizes, cpu limits, expected ports, etc. for various AWS CDK constructs created as part of the Aspire deployment.
    /// It also provides default CDK constructs like VPCs, ECS Clusters, Security Groups, etc. that will be shared across multiple resources.
    /// </para>
    /// <para>
    /// In the CDK stack class you can define your own default resources and apply the AWS Aspire default attribute on the resource to identify the resource
    /// as the default. The following example shows how to define a stack that uses an account's default VPC.
    /// </para>
    /// <code>
    /// public class DeploymentStack : Stack
    /// {
    ///     public DeploymentStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    ///     {
    ///         DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
    ///         {
    ///             IsDefault = true
    ///         });
    ///     }
    ///
    ///     [DefaultVpc]
    ///     public IVpc DefaultVpc { get; private set; }
    /// }
    /// </code>
    /// <para>
    /// The Aspire.Hosting.AWS.Deployment namespace attributes using the naming pattern "Default&lt;resource&gt;Attribute" to identify default resources in the CDK stack.
    /// For example <see cref="Aspire.Hosting.AWS.Deployment.DefaultVpcAttribute"/> is used to identify the default VPC resource in the CDK stack.
    /// </para>
    /// <para>
    /// <see cref="CDKDefaultsProviderFactory.Preview_V1"/> is the latest version of the defaults provider provided by the library. As new versions
    /// are created there can be breaking changes that will be documented for each version. This breaking changes might cause AWS resources to be
    /// removed and recreated during deployment. Users can choose when to upgrade to a new version of the defaults provider by changing the factory.
    /// Users can also extend from the existing versions to create their own custom defaults provider and override any of the default settings.
    /// See <see cref="CDKDefaultsProviderFactory"/> for more details.
    /// </para>
    /// </summary>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="name">The Aspire resource name also used as the CloudFormation stack name.</param>
    /// <param name="cdkDefaultsProviderFactory">The DefaultProvider is used configure the default choices used when deploying resources.</param>
    /// <param name="stackFactory">Func to provide a custom CDK stack with its own resources. The Aspire provisioned resource will be added to this CDK stack.</param>
    /// <param name="environmentResourceConfig">Additional optional configuration for the environment. For example specifying the AWS SDK configuration.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<AWSCDKEnvironmentResource<T>> AddAWSCDKEnvironment<T>(this IDistributedApplicationBuilder builder, [ResourceName] string name, CDKDefaultsProviderFactory cdkDefaultsProviderFactory, Func<App, IStackProps, T> stackFactory, AWSCDKEnvironmentResourceConfig? environmentResourceConfig = null)
        where T : Stack
    {
        builder.AddEnvironmentServices();

        var env = new AWSCDKEnvironmentResource<T>(name, builder.ExecutionContext.IsPublishMode, cdkDefaultsProviderFactory, stackFactory, environmentResourceConfig);

        if (builder.ExecutionContext.IsRunMode)
        {
            return builder.CreateResourceBuilder(env);
        }

        return builder.AddResource(env);
    }

    /// <summary>
    /// Deploy to AWS Elastic Container Service using the <see href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html">Express Mode</see>.
    /// Express mode deploys as an ECS service and a shared Application Load Balancer (ALB) across your Express mode services to route traffic to the service. 
    /// An HTTPS endpoint will be provisioned by default and a TargetGroup rule added to the ALB for the provisioned host name.
    /// The CDK <see href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.CfnExpressGatewayService.html">CfnExpressGatewayService</see> construct is used to create the ECS Express Gateway service.
    /// </summary>
    /// <remarks>
    /// Port 8080 is assumed to be the container port the web application listens on. This can be customized by adding a callback on the config's PropsCfnExpressGatewayServicePropsCallback property.
    /// </remarks>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateExpressService(this IResourceBuilder<ProjectResource> builder, PublishECSFargateExpressServiceConfig? config = null)
    {
        var annotation = new PublishECSFargateServiceExpressAnnotation { Config = config ?? new PublishECSFargateExpressServiceConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Deploy to AWS ECS Fargate Service. An ECS service is a continuously running set of tasks running the console application as a container.
    /// The CDK <see href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.FargateService.html">FargateService</see> construct is used to create the ECS service.
    /// </summary>
    /// <remarks>
    /// No HTTP(S) endpoint is provisioned for this publish target.
    /// </remarks>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateService(this IResourceBuilder<ProjectResource> builder, PublishECSFargateServiceConfig? config = null)
    {
        var annotation = new PublishECSFargateServiceAnnotation { Config = config ?? new PublishECSFargateServiceConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Deploy to AWS ECS Fargate Service with Application Load Balancer. This uses the CDK 
    /// <see href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs_patterns.ApplicationLoadBalancedFargateService.html">
    /// ApplicationLoadBalancedFargateService</see> construct. This construct will create an ECS Fargate service fronted by an 
    /// Application Load Balancer (ALB) to distribute incoming traffic across multiple instances of the web application.
    /// By default, an HTTP endpoint will be provisioned.
    /// </summary>
    /// <remarks>
    /// Port 8080 is assumed to be the container port the web application listens on. This can be customized by adding a callback on the config's PropsApplicationLoadBalancedTaskImageOptionsCallback property.
    /// </remarks>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ProjectResource> PublishAsECSFargateServiceWithALB(this IResourceBuilder<ProjectResource> builder, PublishECSFargateServiceWithALBConfig? config = null)
    {
        var annotation = new PublishECSFargateServiceWithALBAnnotation { Config = config ?? new PublishECSFargateServiceWithALBConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Configures the <see cref="RedisResource"/> to be published as an Amazon ElastiCache provisioned node cluster during deployment.
    /// </summary>
    /// <remarks>
    /// For optimal AWS support Valkey will be used by default as the cluster engine. To use Redis as the cluster engine set a callback on the config's <see cref="PublishElastiCacheProvisionClusterConfig.PropsCfnReplicationGroupCallback"/>
    /// property and modify the <see cref="Amazon.CDK.AWS.ElastiCache.CfnReplicationGroupProps.Engine"/> property.
    /// </remarks>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<RedisResource> PublishAsElasticCacheProvisionCluster(this IResourceBuilder<RedisResource> builder, PublishElastiCacheProvisionClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheProvisionClusterAnnotation { Config = config ?? new PublishElastiCacheProvisionClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Configures the <see cref="RedisResource"/> to be published as an Amazon ElastiCache serverless cluster during deployment.
    /// </summary>
    /// <remarks>
    /// For optimal AWS support Valkey will be used by default as the cluster engine. To use Redis as the cluster engine set a callback on the config's <see cref="PublishElastiCacheServerlessClusterConfig.PropsCfnServerlessCacheCallback"/>
    /// property and modify the <see cref="Amazon.CDK.AWS.ElastiCache.CfnServerlessCacheProps.Engine"/> property.
    /// </remarks>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<RedisResource> PublishAsElasticCacheServerlessCluster(this IResourceBuilder<RedisResource> builder, PublishElastiCacheServerlessClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheServerlessClusterAnnotation { Config = config ?? new PublishElastiCacheServerlessClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Configures the <see cref="ValkeyResource"/> to be published as an Amazon ElastiCache provisioned node cluster during deployment.
    /// </summary>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ValkeyResource> PublishAsElasticCacheProvisionCluster(this IResourceBuilder<ValkeyResource> builder, PublishElastiCacheProvisionClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheProvisionClusterAnnotation { Config = config ?? new PublishElastiCacheProvisionClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Configures the <see cref="ValkeyResource"/> to be published as an Amazon ElastiCache serverless cluster during deployment.
    /// </summary>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">An optional configuration object for providing callbacks to customize the CDK props and construct.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<ValkeyResource> PublishAsElasticCacheServerlessCluster(this IResourceBuilder<ValkeyResource> builder, PublishElastiCacheServerlessClusterConfig? config = null)
    {
        var annotation = new PublishElasticCacheServerlessClusterAnnotation { Config = config ?? new PublishElastiCacheServerlessClusterConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }

    /// <summary>
    /// Deploy project to AWS Lambda as a function.
    /// The CDK <see href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_lambda.Function.html">Function</see> construct is used to create the Lambda function.
    /// </summary>
    /// <remarks>
    /// To configure event sources use the <see cref="PublishLambdaFunctionConfig.ConstructFunctionCallback"/> property to access the CDK construct and add event sources.
    /// The following example shows how to add an SQS event source to the Lambda function:
    /// </remarks>
    /// <code>
    /// builder.AddAWSLambdaFunction&lt;Projects.SQSProcessorFunction&gt;("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
    ///     .PublishAsLambdaFunction(new PublishLambdaFunctionConfig
    ///     {
    ///         ConstructFunctionCallback = (ctx, construct) =>
    ///         {
    ///             construct.AddEventSource(new SqsEventSource(ctx.GetDeploymentStack&lt;DeploymentStack&gt;().LambdaQueue, new SqsEventSourceProps
    ///             {
    ///                 BatchSize = 5,
    ///                 Enabled = true
    ///             }));
    ///         }
    ///     });
    /// </code>
    /// <param name="builder">The resource builder for the resource to configure.</param>
    /// <param name="config">Configuration for attaching callbacks to configure the CDK construct's props and associate the created CDK construct to other CDK constructs.</param>
    /// <returns>The same resource builder instance for chaining additional configuration.</returns>
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public static IResourceBuilder<LambdaProjectResource> PublishAsLambdaFunction(this IResourceBuilder<LambdaProjectResource> builder, PublishLambdaFunctionConfig? config = null)
    {
        var annotation = new PublishLambdaFunctionAnnotation { Config = config ?? new PublishLambdaFunctionConfig() };
        builder.Resource.Annotations.Add(annotation);

        return builder;
    }    
}
