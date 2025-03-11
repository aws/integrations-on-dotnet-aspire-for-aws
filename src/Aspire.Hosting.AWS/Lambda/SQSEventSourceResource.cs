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
}
