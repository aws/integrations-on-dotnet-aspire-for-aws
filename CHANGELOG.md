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
