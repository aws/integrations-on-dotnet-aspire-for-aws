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
#pragma warning disable ASPIRECOMPUTE001

var builder = DistributedApplication.CreateBuilder(args);

var awsEnvironment = builder.AddAWSCDKEnvironment("aws", DeploymentComputeService.ECSFargate, DefaultProvider.V1, app => new DeploymentStack(app, "DeploymentInfrastructure3"));
var deploymentStack = awsEnvironment.Resource.EnvironmentStack;
var deploymentTag = "v" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

var cache = builder.AddRedis("cache");

var frontend = builder.AddProject<Projects.Frontend>("Frontend")
        .WithDeploymentImageTag(context => deploymentTag)
        .WithReference(cache)
        .WaitFor(cache)
        // Add callback to customize the default settings of the CDK construct created for this project.
        .PublishAsECSFargateServiceWithALB(new PublishCDKECSFargateWithALBConfig
        {
            PropsApplicationLoadBalancedFargateServiceCallback = props =>
            {
                props.DesiredCount = 1;
            },
            ConstructApplicationLoadBalancedFargateServiceCallback = construct =>
            {
                // For faster dev turn around set deregistration to a short time
                construct.TargetGroup.SetAttribute("deregistration_delay.timeout_seconds", "10");
                // Enable cookie stickiness for session affinity for 24 hours
                construct.TargetGroup.EnableCookieStickiness(Duration.Seconds(86400));
            }
        });

builder.AddProject<Projects.Backend>("backend")
        .WithDeploymentImageTag(context => deploymentTag)
        .WithReference(frontend)
        .WithReference(cache)
        .WaitFor(cache);

builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .WithDeploymentImageTag(context => deploymentTag)
        .PublishAsLambdaFunction(new PublishCDKLambdaConfig
        {
            PropsFunctionCallback = props =>
            {
                props.Vpc = awsEnvironment.Resource.DeploymentConstructProvider.GetDefaultVpc();
                props.SecurityGroups = new[] { awsEnvironment.Resource.DeploymentConstructProvider.GetDefaultElastiCacheSecurityGroup() };
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
 