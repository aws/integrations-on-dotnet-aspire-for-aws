// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.AWS.Lambda;
using Constructs;
using System.Runtime.Versioning;
using System.Text;

#pragma warning disable IDE0130
namespace Aspire.Hosting;

/// <summary>
/// Extension methods adding SQS event source for Lambda functions.
/// </summary>
[RequiresPreviewFeatures(Constants.LambdaPreviewMessage)]
public static class SQSEventSourceExtensions
{
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
        return WithSQSEventSource(lambdaFunction, () => ValueTask.FromResult(queueUrl), options);
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
        var queueOutputReference = queue.GetOutput("QueueUrl", queue => queue.QueueUrl);
        return WithSQSEventSource(lambdaFunction, queueOutputReference, options);
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

        return WithSQSEventSource(lambdaFunction, resolver, options);
    }

    private static IResourceBuilder<LambdaProjectResource> WithSQSEventSource(IResourceBuilder<LambdaProjectResource> lambdaFunction, Func<ValueTask<string>> queueUrlResolver, SQSEventSourceOptions? options = null)
    {
        var sqsEventSourceResource = lambdaFunction.ApplicationBuilder.AddResource(new SQSEventSourceResource($"SQSEventSource-{lambdaFunction.Resource.Name}"))
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

            var configBuilder = new StringBuilder();
            configBuilder.Append($"QueueUrl={queueUrl},FunctionName={lambdaFunction.Resource.Name},LambdaRuntimeApi={lambdaEmulatorAnnotation.Endpoint.Url}");

            if (options != null)
            {
                if (options.BatchSize.HasValue)
                {
                    configBuilder.Append($",BatchSize={options.BatchSize.Value}");
                }
                if (options.DisableMessageDelete.HasValue)
                {
                    configBuilder.Append($",DisableMessageDelete={options.DisableMessageDelete.Value}");
                }
                if (options.VisibilityTimeout.HasValue)
                {
                    configBuilder.Append($",VisibilityTimeout={options.VisibilityTimeout.Value}");
                }
            }

            // Look to see if the Lambda function has been configured with an AWS SDK config. If so then
            // configure the SQS event source with the same config to access the SQS queue.
            var awsSdkConfig = lambdaFunction.Resource.Annotations.OfType<SDKResourceAnnotation>().FirstOrDefault()?.SdkConfig;
            if (awsSdkConfig != null)
            {
                if (!string.IsNullOrEmpty(awsSdkConfig.Profile))
                {
                    configBuilder.Append($",Profile={awsSdkConfig.Profile}");
                }
                if (awsSdkConfig.Region != null)
                {
                    configBuilder.Append($",Region={awsSdkConfig.Region.SystemName}");
                }
            }

            context.EnvironmentVariables[SQSEventSourceResource.SQS_EVENT_CONFIG_ENV_VAR] = configBuilder.ToString();
        });

        return lambdaFunction;
    }
}
