// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.AWS.Lambda;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

/// <summary>
/// Extension methods adding DynamoDB Streams event source for Lambda functions.
/// </summary>
public static class DynamoDBStreamsEventSourceExtensions
{
    private const int MaxResourceNameLength = 64;

    /// <summary>
    /// Add a DynamoDB Streams event source to a Lambda function. This feature emulates adding a DynamoDB Streams event source to a Lambda function when deployed to AWS.
    /// A separate sub resource will be added to the .NET Aspire application that polls the DynamoDB stream. As records
    /// are received from the stream the Lambda function will be invoked with the records.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="tableName">The name of the DynamoDB table to read stream records from.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    public static IResourceBuilder<LambdaProjectResource> WithDynamoDBStreamsEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, string tableName, DynamoDBStreamsEventSourceOptions? options = null)
    {
        return WithDynamoDBStreamsEventSource(lambdaFunction, () => ValueTask.FromResult(tableName), options, tableName: tableName);
    }

    /// <summary>
    /// Add a DynamoDB Streams event source to a Lambda function. This feature emulates adding a DynamoDB Streams event source to a Lambda function when deployed to AWS.
    /// A separate sub resource will be added to the .NET Aspire application that polls the DynamoDB stream. As records
    /// are received from the stream the Lambda function will be invoked with the records.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="table">CDK DynamoDB Table construct to read stream records from.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    public static IResourceBuilder<LambdaProjectResource> WithDynamoDBStreamsEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, IResourceBuilder<IConstructResource<Amazon.CDK.AWS.DynamoDB.Table>> table, DynamoDBStreamsEventSourceOptions? options = null)
    {
        var tableNameOutputReference = table.GetOutput("TableName", t => t.TableName);
        var tableName = table.Resource.Name;
        Func<ValueTask<string>> resolver = async () =>
        {
            var resolvedTableName = await tableNameOutputReference.GetValueAsync();
            if (string.IsNullOrEmpty(resolvedTableName))
            {
                throw new InvalidOperationException("Output parameter for table name failed to resolve");
            }

            return resolvedTableName;
        };
        return WithDynamoDBStreamsEventSource(lambdaFunction, resolver, options, tableName);
    }

    /// <summary>
    /// Add a DynamoDB Streams event source to a Lambda function. This feature emulates adding a DynamoDB Streams event source to a Lambda function when deployed to AWS.
    /// A separate sub resource will be added to the .NET Aspire application that polls the DynamoDB stream. As records
    /// are received from the stream the Lambda function will be invoked with the records.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="tableNameCfnOutputReference">CloudFormation StackOutputReference that should point to a DynamoDB table name output parameter in the CloudFormation stack.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    public static IResourceBuilder<LambdaProjectResource> WithDynamoDBStreamsEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, StackOutputReference tableNameCfnOutputReference, DynamoDBStreamsEventSourceOptions? options = null)
    {
        Func<ValueTask<string>> resolver = async () =>
        {
            var tableName = await tableNameCfnOutputReference.GetValueAsync();
            if (string.IsNullOrEmpty(tableName))
            {
                throw new InvalidOperationException("Output parameter for table name failed to resolve");
            }

            return tableName;
        };

        return WithDynamoDBStreamsEventSource(lambdaFunction, resolver, options, tableNameCfnOutputReference.Name);
    }

    private static IResourceBuilder<LambdaProjectResource> WithDynamoDBStreamsEventSource(IResourceBuilder<LambdaProjectResource> lambdaFunction, Func<ValueTask<string>> tableNameResolver, DynamoDBStreamsEventSourceOptions? options = null, string? tableName = null)
    {
        var lambdaName = lambdaFunction.Resource.Name;
        var resourceName = !string.IsNullOrEmpty(options?.ResourceName)
            ? options.ResourceName
            : !string.IsNullOrEmpty(tableName)
                ? $"DDBStreamsEventSource-{lambdaName}-{tableName}"
                : $"DDBStreamsEventSource-{lambdaName}";

        resourceName = EnsureResourceNameLength(resourceName, lambdaName, tableName);

        var dynamoDBStreamsEventSourceResource = lambdaFunction.ApplicationBuilder.AddResource(new DynamoDBStreamsEventSourceResource(resourceName))
                                    .WithParentRelationship(lambdaFunction)
                                    .ExcludeFromManifest();

        dynamoDBStreamsEventSourceResource.WithArgs(context =>
        {
            dynamoDBStreamsEventSourceResource.Resource.AddCommandLineArguments(context.Args);
        });

        dynamoDBStreamsEventSourceResource.WithEnvironment(async (context) =>
        {
            LambdaEmulatorAnnotation? lambdaEmulatorAnnotation = null;
            if (lambdaFunction.ApplicationBuilder.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out lambdaEmulatorAnnotation)) == null ||
                    lambdaEmulatorAnnotation == null)
            {
                throw new InvalidOperationException("Lambda function is missing required annotations for Lambda emulator");
            }

            var resolvedTableName = await tableNameResolver();

            // Look to see if the Lambda function has been configured with an AWS SDK config. If so then
            // configure the DynamoDB Streams event source with the same config to access the DynamoDB stream.
            var awsSdkConfig = lambdaFunction.Resource.Annotations.OfType<SDKResourceAnnotation>().FirstOrDefault()?.SdkConfig;

            var dynamoDBStreamsEventConfig = DynamoDBStreamsEventSourceResource.CreateDynamoDBStreamsEventConfig(resolvedTableName, lambdaFunction.Resource.Name, lambdaEmulatorAnnotation.LambdaRuntimeEndpoint.Url, options, awsSdkConfig);
            context.EnvironmentVariables[DynamoDBStreamsEventSourceResource.DYNAMODB_STREAMS_EVENT_CONFIG_ENV_VAR] = dynamoDBStreamsEventConfig;

            if (lambdaFunction.Resource.DynamoDBLocalInstance != null)
            {
                // If the Lambda function has a reference to a DynamoDB local instance, then set the AWS_ENDPOINT_URL_DYNAMODB_STREAMS and AWS_ENDPOINT_URL_DYNAMODB_STREAMS environment variables to the endpoint of the DynamoDB local container.
                // This will allow the DynamoDB Streams event source to connect to the DynamoDB local instance when polling for stream records.
                var dynamoDBLocalEndpoint = lambdaFunction.Resource.DynamoDBLocalInstance.GetEndpoint("http");
                context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB"] = dynamoDBLocalEndpoint;
                context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"] = dynamoDBLocalEndpoint;
            }
        });

        return lambdaFunction;
    }

    /// <summary>
    /// Ensures the resource name does not exceed Aspire's 64-character limit.
    /// When truncation is needed, uses a short hash of the table name to preserve uniqueness.
    /// </summary>
    private static string EnsureResourceNameLength(string resourceName, string lambdaName, string? tableName)
    {
        if (resourceName.Length <= MaxResourceNameLength)
            return resourceName;

        if (!string.IsNullOrEmpty(tableName))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tableName)))[..8].ToLowerInvariant();

            if (MaxResourceNameLength <= hash.Length)
            {
                return hash[..MaxResourceNameLength];
            }

            var prefix = $"DDBStreamsEventSource-{lambdaName}-";
            var maxPrefixLength = MaxResourceNameLength - hash.Length;

            if (prefix.Length > maxPrefixLength)
            {
                prefix = prefix[..maxPrefixLength];
            }

            return prefix + hash;
        }

        return resourceName[..MaxResourceNameLength];
    }
}
