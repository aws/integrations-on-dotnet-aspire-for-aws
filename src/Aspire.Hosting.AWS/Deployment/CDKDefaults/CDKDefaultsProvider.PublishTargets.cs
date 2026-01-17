// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
   /// <summary>
    /// Specifies the available publishing targets for a <see cref="Aspire.Hosting.ApplicationModel.ProjectResource">ProjectResource</see> with
    /// endpoints defined implying the resource is web application.
    /// </summary>
    public enum WebProjectResourcePublishTarget 
    {
        /// <summary>
        /// Deploy to AWS Elastic Container Service using the <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html">Express Mode</a>.
        /// Express mode deploys as an ECS service and a shared Application Load Balancer (ALB) across your Express mode services to route traffic to the service. 
        /// An HTTPS endpoint will be provisioned by default and a TargetGroup rule added to the ALB for the provisioned host name.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.CfnExpressGatewayService.html">CfnExpressGatewayService</a> construct is used to create the ECS Express Gateway service.
        /// </summary>
        /// <remarks>
        /// Port 8080 is assumed to be the container port the web application listens on.
        /// </remarks>
        ECSFargateExpressService,

        /// <summary>
        /// Deploy to AWS ECS Fargate Service with Application Load Balancer. This uses the CDK 
        /// <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs_patterns.ApplicationLoadBalancedFargateService.html">
        /// ApplicationLoadBalancedFargateService</a> construct. This construct will create an ECS Fargate service fronted by an 
        /// Application Load Balancer (ALB) to distribute incoming traffic across multiple instances of the web application.
        /// By default an HTTP endpoint will be provisioned.
        /// </summary>
        /// <remarks>
        /// Port 8080 is assumed to be the container port the web application listens on.
        /// </remarks>
        ECSFargateServiceWithALB
    }

    /// <summary>
    /// The default publishing target to use when publishing <see cref="Aspire.Hosting.ApplicationModel.ProjectResource">ProjectResource</see>
    /// with endpoints defined implying a web application. The default value is <see cref="WebProjectResourcePublishTarget.ECSFargateExpressService"/>.
    /// </summary>
    public virtual WebProjectResourcePublishTarget DefaultWebProjectResourcePublishTarget { get; set; } = WebProjectResourcePublishTarget.ECSFargateExpressService;

    /// <summary>
    /// Specifies the available publishing targets for <see cref="Aspire.Hosting.ApplicationModel.ProjectResource">ProjectResource</see> with no endpoints defined.
    /// </summary>
    public enum ConsoleProjectResourcePublishTarget 
    {
        /// <summary>
        /// Deploy as a service to AWS Elastic Container Service (ECS). An ECS service is a continuously running set of tasks running the console application as a container.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.FargateService.html">FargateService</a> construct is used to create the ECS service.
        /// </summary>
        ECSFargateService
    }

    /// <summary>
    /// The default publishing target to use when publishing <see cref="Aspire.Hosting.ApplicationModel.ProjectResource">ProjectResource</see>  with no endpoints defined. For example background
    /// workers or message processors. The default value is <see cref="ConsoleProjectResourcePublishTarget.ECSFargateService"/>.
    /// </summary>
    public virtual ConsoleProjectResourcePublishTarget DefaultConsoleProjectResourcePublishTarget { get; set; } = ConsoleProjectResourcePublishTarget.ECSFargateService;

    /// <summary>
    /// Specifies the available publishing targets <see cref="Aspire.Hosting.AWS.Lambda.LambdaProjectResource">LambdaProjectResource</see>.
    /// </summary>
    public enum LambdaProjectResourcePublishTarget 
    {
        /// <summary>
        /// Deploy <see cref="Aspire.Hosting.AWS.Lambda.LambdaProjectResource">LambdaProjectResource</see> to AWS Lambda as a Lambda Function.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_lambda.Function.html">Function</a> construct is used to create the Lambda function.
        /// </summary>
        LambdaFunction
    }

    /// <summary>
    /// The default publishing target to use when publishing <see cref="Aspire.Hosting.AWS.Lambda.LambdaProjectResource">LambdaProjectResource</see>. The default value is <see cref="LambdaProjectResourcePublishTarget.LambdaFunction"/>.
    /// </summary>
    public virtual LambdaProjectResourcePublishTarget DefaultLambdaProjectResourcePublishTarget { get; set; } = LambdaProjectResourcePublishTarget.LambdaFunction;

    public enum RedisResourcePublishTarget
    {
        /// <summary>
        /// Creates an Amazon ElastiCache Provisioned Cluster to host the Redis or Valkey resource.
        /// </summary>
        ElastiCacheProvisionCluster,

        /// <summary>
        /// Creates an Amazon ElastiCache for Serverless Cluster to host the Redis or Valkey resource.
        /// </summary>
        ElastiCacheServerlessCluster
    }

    /// <summary>
    /// The default publishing target to use when publishing <see cref="Aspire.Hosting.ApplicationModel.RedisResource">RedisResource</see> or <see cref="Aspire.Hosting.ApplicationModel.ValkeyResource">ValkeyResource</see>. The default value is <see cref="RedisResourcePublishTarget.ElastiCacheServerlessCluster"/>.
    /// </summary>
    public virtual RedisResourcePublishTarget DefaultRedisResourcePublishTarget { get; set; } = RedisResourcePublishTarget.ElastiCacheServerlessCluster;
}
