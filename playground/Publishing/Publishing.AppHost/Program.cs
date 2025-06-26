// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Environments;
using Lambda.AppHost;

// TODOs:
// WithReferences to other projects for service discovery
// Support Publish methods from AddContainer
// Handle the AWS application resources provisioned by the AddAWSCDKStack so they are included in the deployment
// Provisioning RDS databases and connecting via WithReference
// Projects deployed to Beanstalk
// Parameter hints, Having the AddParameter hint that the parameter is something like a VPC
// Enabling OTEL collection to CloudWatch
// Create API Gateway via the emulator's configuration
// Look into Serverless ElastiCache cluster

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

var deploymentStack = (builder.AddAWSCDKEnvironment("aws", app => new DeploymentStack(app, "DeploymentInfrastructure7"))).Resource.EnvironmentStack;

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

var cache = builder.AddRedis("cache")
                   .PublishAsElasticCacheCluster(new PublishCDKElasticCacheRedisConfig
                   {
                       Engine = PublishCDKElasticCacheRedisConfig.EngineType.Redis,
                       EngineVersion = "7.1",
                       CacheNodeType = "cache.t3.micro",
                       CacheParameterGroupName = deploymentStack.ElastiCacheParameterGroup.Ref,
                       CacheSubnetGroupName = deploymentStack.ElastiCacheSubnetGroup.Ref,
                       SecurityGroupIds = new[] {deploymentStack.DefaultSecurityGroup.SecurityGroupId } 
                   });

builder.AddProject<Projects.Frontend>("Frontend")
        .WithReference(cache)
        .WaitFor(cache)
        .PublishAsECSFargateServiceWithALB(new PublishCDKECSFargateWithALBConfig
        {
            ECSCluster = deploymentStack.ECSCluster,
            PropsApplicationLoadBalancedFargateServiceCallback = props =>
            {
                props.MemoryLimitMiB = 512;
                props.SecurityGroups = new[] { deploymentStack.DefaultSecurityGroup };
            },
            ConstructApplicationLoadBalancedFargateServiceCallback = construct =>
            {
                // For faster dev turn around set deregistration to a short time
                construct.TargetGroup.SetAttribute("deregistration_delay.timeout_seconds", "10");
                construct.TargetGroup.EnableCookieStickiness(Duration.Seconds(86400)); // 24 hours
            }
        });

builder.AddProject<Projects.Backend>("backend")
        .PublishAsECSFargateService(new PublishCDKECSFargateConfig
        {
            ECSCluster = deploymentStack.ECSCluster,
            DesiredCount = 2
        })
        .WithReference(cache)
        .WaitFor(cache);

builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .PublishAsLambdaFunction(new PublishCDKLambdaConfig
        {
            PropsFunctionCallback = props =>
            {
                props.Vpc = deploymentStack.MainVpc;
                props.SecurityGroups = new[] { deploymentStack.DefaultSecurityGroup };
                props.MemorySize = 512;
            },
            ConstructFunctionCallback = construct =>
            {
                construct.AddEventSource(new SqsEventSource(deploymentStack.LambdaQueue, new SqsEventSourceProps
                {
                    BatchSize = 5,
                    Enabled = true
                }));
            }
        })
        .WithReference(cache)
        .WithReference(awsSdkConfig)
        .WithSQSEventSource(localDevQueue);


builder.Build().Run();
 