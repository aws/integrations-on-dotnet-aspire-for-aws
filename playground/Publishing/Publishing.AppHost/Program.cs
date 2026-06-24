// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Lambda.EventSources;
using Aspire.Hosting.AWS.Deployment;
using Lambda.AppHost;

// TODOs:
// Support Publish methods from AddContainer
// Handle the AWS application resources provisioned by the AddAWSCDKStack so they are included in the deployment
// Provisioning RDS databases and connecting via WithReference
// Projects deployed to Beanstalk
// Parameter hints, Having the AddParameter hint that the parameter is something like a VPC
// Enabling OTEL collection to CloudWatch

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREAWSAGENTCORE001

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USWest2);

builder.AddAWSCDKEnvironment("aws", 
                                    CDKDefaultsProviderFactory.Preview_V1, 
                                    (app, props) => new DeploymentStack(app, "AspirePlay5", props), 
                                    new AWSCDKEnvironmentResourceConfig { AWSSDKConfig = awsSdkConfig });

var cdkStackResource = builder.AddAWSCDKStack("AWSLambdaPlaygroundResources");
var localDevQueue = cdkStackResource.AddSQSQueue("LocalDevQueue");

var cache = builder.AddValkey("cache");

// ParameterResource examples — these become CloudFormation parameters at deploy time
builder.Configuration["Parameters:api-key"] = "default-api-key";
builder.Configuration["Parameters:db-connection"] = "Server=localhost;Database=mydb";
var apiKey = builder.AddParameter("api-key");
var dbConnection = builder.AddParameter("db-connection", secret: true);

var horoscopeAgent = builder.AddAgentCoreRuntime<Projects.Publishing_HoroscopeAgent>("HoroscopeAgent")
        .WithReference(cache)
        .WaitFor(cache);

var frontend = builder.AddProject<Projects.Frontend>("Frontend")
        .WithEnvironment("ENV_LAMBDA_1", "LambdaValue1")
        .WithEnvironment("API_KEY", apiKey)
        .WithEnvironment(callback: (env) =>
        {
            env.EnvironmentVariables.Add("ENV_LAMBDA_2", "LambdaValue2");
        })
        .WithExternalHttpEndpoints()
        .WithReference(cache)
        .WaitFor(cache);


builder.AddProject<Projects.Backend>("backend")
        .WithEnvironment("ENV_LAMBDA_1", "LambdaValue1")
        .WithEnvironment("DB_CONNECTION", dbConnection)
        .WithEnvironment(callback: (env) =>
        {
            env.EnvironmentVariables.Add("ENV_LAMBDA_2", "LambdaValue2");
        })
        .WithReference(frontend)
        .WithReference(cache)
        .WithReference(horoscopeAgent)
        .WaitFor(cache);

builder.AddAWSLambdaFunction<Projects.SQSProcessorFunction>("SQSProcessorFunction", "SQSProcessorFunction::SQSProcessorFunction.Function::FunctionHandler")
        .PublishAsLambdaFunction(new PublishLambdaFunctionConfig
        {
            ConstructFunctionCallback = (ctx, construct) =>
            {
                construct.AddEventSource(new SqsEventSource(ctx.GetDeploymentStack<DeploymentStack>().LambdaQueue, new SqsEventSourceProps
                {
                    BatchSize = 5,
                    Enabled = true
                }));
            }
        })
        .WithEnvironment("ENV_LAMBDA_1", "LambdaValue1")
        .WithEnvironment("API_KEY", apiKey)
        .WithEnvironment("DB_CONNECTION", dbConnection)
        .WithEnvironment(callback: (env) =>
        {
            env.EnvironmentVariables.Add("ENV_LAMBDA_2", "LambdaValue2");
        })
        .WithReference(awsSdkConfig)
        .WithSQSEventSource(localDevQueue);

builder.Build().Run();
 