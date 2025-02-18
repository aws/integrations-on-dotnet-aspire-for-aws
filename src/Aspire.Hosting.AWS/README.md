# Aspire.Hosting.AWS library

Provides extension methods and resources definition for a .NET Aspire AppHost to configure the AWS SDK for .NET and AWS application resources.

## Prerequisites

- [Configure AWS credentials](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html)
- [Node.js](https://nodejs.org) _(AWS CDK only)_

## Install the package

In your AppHost project, install the `Aspire.Hosting.AWS` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package Aspire.Hosting.AWS
```

## Configuring the AWS SDK for .NET

The AWS profile and region the SDK should use can be configured using the `AddAWSSDKConfig` method.
The following example creates a config using the dev profile from the `~/.aws/credentials` file and points the SDK to the
`us-west-2` region.

```csharp
var awsConfig = builder.AddAWSSDKConfig()
                        .WithProfile("dev")
                        .WithRegion(RegionEndpoint.USWest2);
```

The configuration can be attached to projects using the `WithReference` method. This will set the `AWS_PROFILE` and `AWS_REGION`
environment variables on the project to the profile and region configured by the `AddAWSSDKConfig` method. SDK service clients created in the
project without explicitly setting the credentials and region will pick up these environment variables and use them
to configure the service client.

```csharp
builder.AddProject<Projects.Frontend>("Frontend")
        .WithReference(awsConfig)
```

If a project has a reference to an AWS resource like the AWS CloudFormation resources that have an AWS SDK configuration
the project will infer the AWS SDK configuration from the AWS resource. For example if you call the `WithReference` passing
in the CloudFormation resource then a second `WithReference` call passing in the AWS SDK configuration is not necessary.

## Provisioning application resources with AWS CloudFormation

AWS application resources like Amazon DynamoDB tables or Amazon Simple Queue Service (SQS) queues can be provisioned during AppHost
startup using a CloudFormation template.

In the AppHost project create either a JSON or YAML CloudFormation template. Here is an example template called `app-resources.template` that creates a queue and topic.
```json
{
    "AWSTemplateFormatVersion" : "2010-09-09",
    "Parameters" : {
        "DefaultVisibilityTimeout" : {
            "Type" : "Number",
            "Description" : "The default visibility timeout for messages in SQS queue."
        }
    },
    "Resources" : {
        "ChatMessagesQueue" : {
            "Type" : "AWS::SQS::Queue",
            "Properties" : {
                "VisibilityTimeout" : { "Ref" : "DefaultVisibilityTimeout" }
            }
        },
        "ChatTopic" : {
            "Type" : "AWS::SNS::Topic",
            "Properties" : {
                "Subscription" : [
                    { "Protocol" : "sqs", "Endpoint" : { "Fn::GetAtt" : [ "ChatMessagesQueue", "Arn" ] } }
                ]
            }
        }
    },
    "Outputs" : {
        "ChatMessagesQueueUrl" : {
            "Value" : { "Ref" : "ChatMessagesQueue" }
        },
        "ChatTopicArn" : {
            "Value" : { "Ref" : "ChatTopic" }
        }
    }
}
```

In the AppHost the `AddAWSCloudFormationTemplate` method is used to register the CloudFormation resource. The first parameter,
which is the Aspire resource name, is used as the CloudFormation stack name when the `stackName` parameter is not set.
If the template defines parameters the value can be provided using
the `WithParameter` method. To configure what AWS account and region to deploy the CloudFormation stack,
the `WithReference` method is used to associate a SDK configuration.

```csharp
var awsResources = builder.AddAWSCloudFormationTemplate("AspireSampleDevResources", "app-resources.template")
                          .WithParameter("DefaultVisibilityTimeout", "30")
                          .WithReference(awsConfig);
```

The outputs of a CloudFormation stack can be associated to a project using the `WithReference` method.

```csharp
builder.AddProject<Projects.Frontend>("Frontend")
       .WithReference(awsResources);
```

The output parameters from the CloudFormation stack can be found in the `IConfiguration` under the `AWS:Resources` config section. The config section
can be changed by setting the `configSection` parameter of the `WithReference` method associating the CloudFormation stack to the project.

```csharp
var chatTopicArn = builder.Configuration["AWS:Resources:ChatTopicArn"];
```

Alternatively a single CloudFormation stack output parameter can be assigned to an environment variable using the `GetOutput` method.

```csharp
builder.AddProject<Projects.Frontend>("Frontend")
       .WithEnvironment("ChatTopicArnEnv", awsResources.GetOutput("ChatTopicArn"))
```

## Importing existing AWS resources

To import AWS resources that were created by a CloudFormation stack outside the AppHost the `AddAWSCloudFormationStack` method can be used.
It will associate the outputs of the CloudFormation stack the same as the provisioning method `AddAWSCloudFormationTemplate`.

```csharp
var awsResources = builder.AddAWSCloudFormationStack("ExistingStackName")
                          .WithReference(awsConfig);

builder.AddProject<Projects.Frontend>("Frontend")
       .WithReference(awsResources);
```

## Provisioning application resources with AWS CDK

Adding [AWS CDK](https://aws.amazon.com/cdk/) to the AppHost makes it possible to provision AWS resources using code. Under the hood AWS CDK is using CloudFormation to create the resources in AWS.

In the AppHost the `AddAWSCDK` methods is used to create a CDK Resources which will hold the constructs for describing the AWS resources.

A number of methods are available to add common resources to the AppHost like S3 Buckets, DynamoDB Tables, SQS Queues, SNS Topics, Kinesis Streams and Cognito User Pools. These resources can be added either the CDK resource or a dedicated stack that can be created.

```csharp
var stack = builder.AddAWSCDKStack("Stack");
var bucket = stack.AddS3Bucket("Bucket");

builder.AddProject<Projects.Frontend>("Frontend")
       .WithReference(bucket);
```

Resources created with these methods can be directly referenced by project resources and common properties like resource names, ARNs or URLs will be made available as configuration environment variables. The default config section will be `AWS:Resources`

Alternative constructs can be created in free form using the `AddConstruct` methods. These constructs can be references with the `WithReference` method and need to be provided with a property selector and an output name. This will make this property available as configuration environment variable

```csharp
var stack = builder.AddAWSCDKStack("Stack");
var constuct = stack.AddConstruct("Construct", scope => new CustomConstruct(scope, "Construct"));

builder.AddProject<Projects.Frontend>("Frontend")
       .WithReference(construct, c => c.Url, "Url");
```

## Integrating AWS Lambda Local Development

You can develop and test AWS Lambda functions locally within your .NET Aspire application. This enables testing Lambda functions alongside other application resources during development.

![\[\]!(.)](Resources/lambda.png)

### Adding Lambda Functions

To add a Lambda function to your .NET Aspire AppHost, use the `AddAWSLambdaFunction` method. The method supports both executable Lambda functions and class library Lambda functions:

```csharp
// Add an executable Lambda function
builder.AddAWSLambdaFunction<Projects.ExecutableLambdaFunction>(
    "MyLambdaFunction", 
    handler: "ExecutableLambdaFunction")
    .WithReference(awsConfig);

// Add a class library Lambda function
builder.AddAWSLambdaFunction<Projects.ClassLibraryLambdaFunction>(
    "MyLambdaFunction", 
    handler: "ClassLibraryLambdaFunction::ClassLibraryLambdaFunction.Function::FunctionHandler")
    .WithReference(awsConfig);

```

The handler parameter specifies the Lambda handler in different formats depending on the project type:

- For executable projects: specify the assembly name.
- For class library projects: use the format `{assembly}::{type}::{method}`.

### API Gateway Local Emulation

To add an API Gateaway emulator to your .NET Aspire AppHost, use the `AddAPIGatewayEmulator` method. 

```csharp
// Add Lambda functions
var rootWebFunction = builder.AddAWSLambdaFunction<Projects.WebApiLambdaFunction>(
    "RootLambdaFunction", 
    handler: "WebApiLambdaFunction");

var addFunction = builder.AddAWSLambdaFunction<Projects.WebAddLambdaFunction>(
    "AddLambdaFunction", 
    handler: "WebAddLambdaFunction");

// Configure API Gateway emulator
builder.AddAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(rootWebFunction, Method.Get, "/")
    .WithReference(addFunction, Method.Get, "/add/{x}/{y}");
```

The `AddAPIGatewayEmulator` method requires:

- A name for the emulator resource
- The API Gateway type (`Rest`, `HttpV1`, or `HttpV2` )

Use the `WithReference` method to connect Lambda functions to HTTP routes, specifying:

- The Lambda function resource
- The HTTP method
- The route pattern

## Integrating Amazon DynamoDB Local  

Amazon DynamoDB provides a [local version of DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocalHistory.html) for development and testing that is distributed as a container. With version 9.1.0 of the Aspire.Hosting.AWS package, you can easily integrate the DynamoDB local container with your .NET Aspire project. This enables seamless transition between DynamoDB Local for development and the production DynamoDB service in AWS, without requiring any code changes in your application.

To get started in the .NET Aspire AppHost, call the `AddAWSDynamoDBLocal` method to add DynamoDB local as a resource to the .NET Aspire application.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add a DynamoDB Local instance
var localDynamoDB = builder.AddAWSDynamoDBLocal("DynamoDBLocal");
```

For each .NET project in the .NET Aspire application using DynamoDB, add a reference to the DynamoDB local resource.

```csharp
// Reference DynamoDB local in project
builder.AddProject<Projects.Frontend>("Frontend")
   .WithReference(localDynamoDB);
```

In the .NET projects that use DynamoDB, you need to construct the DynamoDB service client from the SDK without explicitly setting the AWS Region or service endpoint. This means constructing the `AmazonDynamoDBClient` object without passing in the Region or an `AmazonDynamoDBConfig` with the `RegionEndpoint` property set. By not explicitly setting the Region, the SDK searches the environment for configuration that informs the SDK where to send the requests. The Region is set locally by the `AWS_REGION` environment variable or in your credentials profile by setting the region property. Once deployed to AWS, the compute environments set environment configuration such as the `AWS_REGION` environment variable so that the SDK knows what Region to use for the service client.

The AWS SDKs have a feature called [Service-specific endpoints](https://docs.aws.amazon.com/sdkref/latest/guide/feature-ss-endpoints.html) that allow setting an endpoint for a service via an environment variable. The `WithReference` call made on the .NET project sets the `AWS_ENDPOINT_URL_DYNAMODB` environment variable. It will be set to the DynamoDB local container that was started as part of the `AddAWSDynamoDBLocal` method.

![\[\]!(.)](Resources/dynamo.png)

The `AWS_ENDPOINT_URL_DYNAMODB` environment variable overrides other config settings like the `AWS_REGION` environment variable, ensuring your projects running locally use DynamoDB local. After the `AmazonDynamoDBClient` has been created pointing to DynamoDB local, all other service calls work the same as if you are going to the real DynamoDB service. No code changes are required.

### Options for DynamoDB Local

When the `AddAWSDynamoDBLocal` method is called, any data and table definitions are stored in memory by default. This means that every time the .NET Aspire application is started, DynamoDB local is initiated with a fresh instance with no tables or data. The `AddAWSDynamoDBLocal` method takes in an optional `DynamoDBLocalOptions` object that exposes the [options](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.UsageNotes.html) that are available for DynamoDB local.

If you want the tables and data to persist between .NET Aspire debug sessions, set the `LocalStorageDirectory` property on the `DynamoDBLocalOptions` object to a local folder where the data will be persisted. The `AddAWSDynamoDBLocal` method will take care of mounting the local directory to the container and configuring the DynamoDB local process to use the mount point.

## Feedback & contributing

https://github.com/dotnet/aspire
