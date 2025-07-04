// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
using Amazon;
using Aspire.Hosting.AWS.DynamoDB;

var builder = DistributedApplication.CreateBuilder(args);

// Setup a configuration for the AWS .NET SDK.
var awsConfig = builder.AddAWSSDKConfig()
                        .WithRegion(RegionEndpoint.USWest2);

// Provision application level resources like SQS queues and SNS topics defined in the CloudFormation template file app-resources.template.
var awsResources = builder.AddAWSCloudFormationTemplate("AspireSampleDevResources", "app-resources.template")
                        .WithParameter("DefaultVisibilityTimeout", "30")
                        // Add the SDK configuration so the AppHost knows what account/region to provision the resources.
                        .WithReference(awsConfig);

awsResources.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

// Add a DynamoDB Local instance
var localDynamoDB = builder.AddAWSDynamoDBLocal("DynamoDBLocal");

// To add outputs of a CloudFormation stack that was created outside of AppHost use the AddAWSCloudFormationStack method.
// then attach the CloudFormation resource to a project using the WithReference method.
//var awsExistingResource = builder.AddAWSCloudFormationStack("ExistingStackName")
//                        .WithReference(awsConfig);

// The AWS SDK Config reference is inferred from the CloudFormation resource associated to the project. If the
// project doesn't have a CloudFormation resource the AWS SDK Config reference can be assigned using the
// WithReference method.
builder.AddProject<Projects.Frontend>("Frontend")
       .WithExternalHttpEndpoints()
        // Demonstrating binding all of the output variables to a section in IConfiguration. By default they are bound to the AWS::Resources prefix.
        // The prefix is configurable by the optional configSection parameter.
        .WithReference(awsResources)
        // Demonstrating binding a single output variable to environment variable in the project.
        .WithEnvironment("ChatTopicArnEnv", awsResources.GetOutput("ChatTopicArn"))
        .WithReference(localDynamoDB);

builder.Build().Run();
