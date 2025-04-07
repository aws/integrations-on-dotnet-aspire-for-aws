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
