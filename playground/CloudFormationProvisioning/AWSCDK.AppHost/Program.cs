using Amazon;
using Amazon.CDK.AWS.SecretsManager;
using AWSCDK.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Setup a configuration for the AWS .NET SDK.
var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.USWest2);

var stack = builder.AddAWSCDKStack("stack", "Aspire-stack").WithReference(awsConfig);
var customStack = builder.AddAWSCDKStack("custom", scope => new CustomStack(scope, "Aspire-custom"));
customStack.AddOutput("BucketName", stack => stack.Bucket.BucketName).WithReference(awsConfig);
customStack.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

// Secrets Manager demo stack
var secretsStack = builder.AddAWSCDKStack("secrets", scope => new SecretsStack(scope, "Aspire-secrets"));
secretsStack.WithReference(awsConfig);
secretsStack.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

// Add individual secrets to the main stack
var apiSecret = stack.AddSecret("ApiSecret");
var dbSecret = stack.AddGeneratedSecret("DatabaseSecret", new SecretStringGenerator
{
    SecretStringTemplate = "{\"username\":\"appuser\"}",
    GenerateStringKey = "password",
    PasswordLength = 32
}, "Auto-generated database credentials for application");

var topic = stack.AddSNSTopic("topic");
var queue = stack.AddSQSQueue("queue");
topic.AddSubscription(queue);

builder.AddProject<Projects.Frontend>("frontend")
    //.WithReference(stack) // Reference all outputs of a construct
    .WithEnvironment("AWS__Resources__BucketName", customStack.GetOutput("BucketName")) // Reference a construct/stack output
    .WithEnvironment("AWS__Resources__ChatTopicArn", topic, t => t.TopicArn)
    .WithReference(customStack, s => s.Queue.QueueUrl, "QueueUrl", "AWS:Resources:Queue")
    // Reference secrets - both configuration section and direct environment variable patterns
    .WithReference(apiSecret, "Secrets:Api")
    .WithReference(dbSecret, "Secrets:Database")
    .WithReference(secretsStack, s => s.DatabaseCredentials.SecretArn, "DbCredentialsArn", "Database:CredentialsArn")
    .WithSecretReference(apiSecret, "API_SECRET_ARN");

builder.Build().Run();
