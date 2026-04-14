// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.SQS;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.AWS.Lambda;
using Constructs;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

/// <summary>
/// Extension methods adding SQS event source for Lambda functions.
/// </summary>
public static class SQSEventSourceExtensions
{
    private const int MaxResourceNameLength = 64;

    /// <summary>
    /// Add an SQS event source to a Lambda function. This feature emulates adding an SQS event source to a Lambda function when deployed to AWS. 
    /// A separate sub resource will be added to the .NET Aspire application that polls the SQS queue. As messages 
    /// are received from the queue the Lambda function will be invoked with the messages.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="queueUrl">The queue url for an SQS queue that will be polled for messages.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IResourceBuilder<LambdaProjectResource> WithSQSEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, string queueUrl, SQSEventSourceOptions? options = null)
    {
        return WithSQSEventSource(lambdaFunction, () => ValueTask.FromResult(queueUrl), options, queueName: null);
    }

    /// <summary>
    /// Add an SQS event source to a Lambda function. This feature emulates adding an SQS event source to a Lambda function when deployed to AWS. 
    /// A separate sub resource will be added to the .NET Aspire application that polls the SQS queue. As messages 
    /// are received from the queue the Lambda function will be invoked with the messages.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="queue">CDK SQS queue construct that will be polled for messages.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IResourceBuilder<LambdaProjectResource> WithSQSEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, IResourceBuilder<IConstructResource<Amazon.CDK.AWS.SQS.Queue>> queue, SQSEventSourceOptions? options = null)
    {
        var queueOutputReference = queue.GetOutput("QueueUrl", q => q.QueueUrl);
        var queueName = queue.Resource.Name;
        Func<ValueTask<string>> resolver = async () =>
        {
            var queueUrl = await queueOutputReference.GetValueAsync();
            if (string.IsNullOrEmpty(queueUrl))
            {
                throw new InvalidOperationException("Output parameter for queue url failed to resolve");
            }

            if (!Uri.TryCreate(queueUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Output parameter value {queueUrl} is not a SQS queue url.");
            }

            return queueUrl;
        };
        return WithSQSEventSource(lambdaFunction, resolver, options, queueName);
    }

    /// <summary>
    /// Add an SQS event source to a Lambda function. This feature emulates adding an SQS event source to a Lambda function when deployed to AWS. 
    /// A separate sub resource will be added to the .NET Aspire application that polls the SQS queue. As messages 
    /// are received from the queue the Lambda function will be invoked with the messages.
    /// </summary>
    /// <param name="lambdaFunction">The Lambda function to add the event source to.</param>
    /// <param name="queueCfnOutputReference">CloudFormation StackOutputReference that should point to a SQS queue url output parameter in the CloudFormation stack.</param>
    /// <param name="options">Optional configuration for the event source.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IResourceBuilder<LambdaProjectResource> WithSQSEventSource(this IResourceBuilder<LambdaProjectResource> lambdaFunction, StackOutputReference queueCfnOutputReference, SQSEventSourceOptions? options = null)
    {
        Func<ValueTask<string>> resolver = async () =>
        {
            var queueUrl = await queueCfnOutputReference.GetValueAsync();
            if (string.IsNullOrEmpty(queueUrl))
            {
                throw new InvalidOperationException("Output parameter for queue url failed to resolve");
            }

            if (!Uri.TryCreate(queueUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"Output parameter value {queueUrl} is not a SQS queue url.");
            }

            return queueUrl;
        };

        return WithSQSEventSource(lambdaFunction, resolver, options, queueCfnOutputReference.Name);
    }

    private static IResourceBuilder<LambdaProjectResource> WithSQSEventSource(IResourceBuilder<LambdaProjectResource> lambdaFunction, Func<ValueTask<string>> queueUrlResolver, SQSEventSourceOptions? options = null, string? queueName = null)
    {
        var lambdaName = lambdaFunction.Resource.Name;
        var resourceName = !string.IsNullOrEmpty(options?.ResourceName)
            ? options.ResourceName
            : !string.IsNullOrEmpty(queueName)
                ? $"SQSEventSource-{lambdaName}-{queueName}"
                : $"SQSEventSource-{lambdaName}";

        resourceName = EnsureResourceNameLength(resourceName, lambdaName, queueName);

        var sqsEventSourceResource = lambdaFunction.ApplicationBuilder.AddResource(new SQSEventSourceResource(resourceName))
                                    .WithParentRelationship(lambdaFunction)
                                    .ExcludeFromManifest();

        sqsEventSourceResource.WithArgs(context =>
        {
            sqsEventSourceResource.Resource.AddCommandLineArguments(context.Args);
        });

        sqsEventSourceResource.WithEnvironment(async (context) =>
        {
            LambdaEmulatorAnnotation? lambdaEmulatorAnnotation = null;
            if (lambdaFunction.ApplicationBuilder.Resources.FirstOrDefault(x => x.TryGetLastAnnotation<LambdaEmulatorAnnotation>(out lambdaEmulatorAnnotation)) == null ||
                    lambdaEmulatorAnnotation == null)
            {
                throw new InvalidOperationException("Lambda function is missing required annotations for Lambda emulator");
            }

            var queueUrl = await queueUrlResolver();

            // Look to see if the Lambda function has been configured with an AWS SDK config. If so then
            // configure the SQS event source with the same config to access the SQS queue.
            var awsSdkConfig = lambdaFunction.Resource.Annotations.OfType<SDKResourceAnnotation>().FirstOrDefault()?.SdkConfig;

            var sqsEventConfig = SQSEventSourceResource.CreateSQSEventConfig(queueUrl, lambdaFunction.Resource.Name, lambdaEmulatorAnnotation.LambdaRuntimeEndpoint.Url, options, awsSdkConfig);
            context.EnvironmentVariables[SQSEventSourceResource.SQS_EVENT_CONFIG_ENV_VAR] = sqsEventConfig;
        });

        return lambdaFunction;
    }

    /// <summary>
    /// Ensures the resource name does not exceed Aspire's 64-character limit.
    /// When truncation is needed, uses a short hash of the queue name to preserve uniqueness.
    /// </summary>
    private static string EnsureResourceNameLength(string resourceName, string lambdaName, string? queueName)
    {
        if (resourceName.Length <= MaxResourceNameLength)
            return resourceName;

        if (!string.IsNullOrEmpty(queueName))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(queueName)))[..8].ToLowerInvariant();

            // Always keep the hash at the end; truncate only the prefix if needed.
            if (MaxResourceNameLength <= hash.Length)
            {
                // Degenerate case: not enough room for the full hash; use as much as fits.
                return hash[..MaxResourceNameLength];
            }

            var prefix = $"SQSEventSource-{lambdaName}-";
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
