## Release 2026-01-22

### Aspire.Hosting.AWS (9.3.1)
* Update AWSSDK.Core to 4.0.3.3

## Release 2025-10-24

### Aspire.Hosting.AWS (9.3.0)
* Update dependency versions
* [Breaking Change] Changed return type for AddAWSDynamoDBLocal to return IResourceBuilder<DynamoDBLocalResource> from IResourceBuilder<IDynamoDBLocalResource>. This allows access to the inherited APIs from DynamoDBLocalResource's parent ContainerResource type.

## Release 2025-07-02

### Aspire.Hosting.AWS (9.2.6)
* Fixed NullReferenceException thrown when provisioning a Cloud Foundation Tempalte without outputs

## Release 2025-06-27

### Aspire.Hosting.AWS (9.2.5)
* Integrate Lambda Test Tool's feature for allowing saving requests
* Fixed NullReferenceException thrown when provisioning a Cloud Foundation template without any outputs

## Release 2025-06-20

### Aspire.Hosting.AWS (9.2.4)
* Fix concurrency issue when reading output from external processes

## Release 2025-06-10

### Aspire.Hosting.AWS (9.2.3)
* Fixed issue with determining the framework or assembly for Lambda functions causing Lambda functions to fail to start
* Add the option for users to customize cloudformation stack tags

## Release 2025-05-19

### Aspire.Hosting.AWS (9.2.2)
* Update default Amazon.Lambda.TestTool version to 0.10.3
* Add IResourceWithWaitSupport support for CloudFormation provisioning. This is useful when using LocalStack container as the CloudFormation endpoint.

## Release 2025-05-08

### Aspire.Hosting.AWS (9.2.1)
* Add ability to set host port for DynamoDB Local
* Add ability to call WithImagePullPolicy for DynamoDB Local resource
* Add Display Names for API Gateway Emulator and Lambda Service Emulator endpoints
* Add Display Names for AWS CloudFormation Console stack links

## Release 2025-05-06

### Aspire.Hosting.AWS (9.2.0)
* Update to V4 of the AWS SDK for .NET
* Update to Aspire 9.2

## Release 2025-04-23

### Aspire.Hosting.AWS (9.1.9)
* Add option to disable sdk validation

## Release 2025-04-11

### Aspire.Hosting.AWS (9.1.8)
* Fixed issue with duplicate CDK output parameter names being used
* Update Amazon.Lambda.TestTool to version 0.10.1
* Update Amazon.Lambda.RuntimeSupport to version 1.13.0

## Release 2025-04-10

### Aspire.Hosting.AWS (9.1.7)
* Fixed issue with duplicate CDK output parameters attempting to be added

## Release 2025-04-07

### Aspire.Hosting.AWS (9.1.6)
* Add support for configuring a port for API Gateway and Lambda emulators
* Add support for configuring SQS event source for a Lambda function
* Update version of Amazon.Lambda.TestTool to install to version 0.10.0

## Release 2025-03-07

### Aspire.Hosting.AWS (9.1.5)
* Add a parent relationship between lambda emulator and functions
* Use the correct environment variable syntax to fix an issue on Rider macOS

## Release 2025-02-27

### Aspire.Hosting.AWS (9.1.4)
* Version bump Amazon.Lambda.TestTool to 0.9.0
* Update Aspire to 9.1.0 and switched to using LaunchProfileAnnotation instead of reflection

## Release 2025-02-20

### Aspire.Hosting.AWS (9.1.3)
* Add validation of AWS credentials
* Add support for adding Class Library Lambda Functions
* Add ability to configure log format and level for Lambda functions
* Version bump Amazon.Lambda.TestTool to 0.0.3

## Release 2025-02-07

### Aspire.Hosting.AWS (9.1.2)
* Add OpenTelemetry support for Lambda in the Aspire Dashboard
* Automatically install .NET Tool Amazon.Lambda.TestTool when running Lambda functions

## Release 2025-01-31 #2

### Aspire.Hosting.AWS (9.1.1)
* Fix namespace for AddAWSDynamoDBLocal extension method

## Release 2025-01-31

### Aspire.Hosting.AWS (9.1.0)
* First preview of Lambda local development preview. Subscribe to the following GitHub issues for preview progress and information on how to get started: https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17
* Update the project urls for the NuGet package
* Add Amazon DynamoDB Local support
* Fix issue with CloudFormationStack resource not using the override stackname property
* Use Aspire 9's WaitFor mechanism for waiting CloudFormation resource to be running
