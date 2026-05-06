// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using System.Text;

namespace Aspire.Hosting.AWS.Lambda;

internal class DynamoDBStreamsEventSourceResource(string name) : ExecutableResource(name,
        "dotnet",
        Environment.CurrentDirectory
        )
{
    internal const string DYNAMODB_STREAMS_EVENT_CONFIG_ENV_VAR = "DYNAMODB_STREAMS_EVENTSOURCE_CONFIG";
    internal void AddCommandLineArguments(IList<object> arguments)
    {
        arguments.Add("lambda-test-tool");
        arguments.Add("start");
        arguments.Add("--no-launch-window");

        arguments.Add("--dynamodbstreams-eventsource-config");

        // The TestTool will look for the config in the environment variable. The environment variable
        // is used because the config contains information like port numbers and table names from CloudFormation
        // stacks that haven't been resolved at the time of setting up the command line arguments.
        arguments.Add($"env:{DYNAMODB_STREAMS_EVENT_CONFIG_ENV_VAR}");
    }

    internal static string CreateDynamoDBStreamsEventConfig(string tableName, string lambdaFunctionName, string lambdaEmulatorUrl, DynamoDBStreamsEventSourceOptions? options, IAWSSDKConfig? awsSdkConfig)
    {
        var configBuilder = new StringBuilder();
        configBuilder.Append($"TableName={tableName},FunctionName={lambdaFunctionName},LambdaRuntimeApi={lambdaEmulatorUrl}");

        if (options?.BatchSize.HasValue == true)
        {
            configBuilder.Append($",BatchSize={options.BatchSize.Value}");
        }

        if (options?.ShardIteratorType.HasValue == true)
        {
            var iteratorTypeValue = options.ShardIteratorType.Value switch
            {
                DynamoDBStreamsIteratorType.TrimHorizon => "TRIM_HORIZON",
                _ => "LATEST"
            };
            configBuilder.Append($",ShardIteratorType={iteratorTypeValue}");
        }

        if (options?.PollingIntervalMs.HasValue == true)
        {
            configBuilder.Append($",PollingIntervalMs={options.PollingIntervalMs.Value}");
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
