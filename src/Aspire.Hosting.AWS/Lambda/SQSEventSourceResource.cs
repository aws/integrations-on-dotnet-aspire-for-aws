// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using System.Text;

namespace Aspire.Hosting.AWS.Lambda;

internal class SQSEventSourceResource(string name) : ExecutableResource(name,
        "dotnet",
        Environment.CurrentDirectory
        )
{
    internal const string SQS_EVENT_CONFIG_ENV_VAR = "SQS_EVENTSOURCE_CONFIG";
    internal void AddCommandLineArguments(IList<object> arguments)
    {
        arguments.Add("lambda-test-tool");
        arguments.Add("start");
        arguments.Add("--no-launch-window");

        arguments.Add("--sqs-eventsource-config");

        // The TestTool will look for the config in the environment variable. The environment variable
        // is used because the config contains information like port numbers and queue urls from CloudFormation
        // stacks that haven't been resolved at the time of settup up the command line arguments.
        arguments.Add($"env:{SQS_EVENT_CONFIG_ENV_VAR}");
    }

    internal static string CreateSQSEventConfig(string queueUrl, string lambdaFunctionName, string lambdaEmulatorUrl, SQSEventSourceOptions? options, IAWSSDKConfig? awsSdkConfig)
    {
        var configBuilder = new StringBuilder();
        configBuilder.Append($"QueueUrl={queueUrl},FunctionName={lambdaFunctionName},LambdaRuntimeApi={lambdaEmulatorUrl}");

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

        return configBuilder.ToString();
    }
}
