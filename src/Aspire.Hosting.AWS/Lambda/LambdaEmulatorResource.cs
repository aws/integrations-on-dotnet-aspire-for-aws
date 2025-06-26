// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;


/// <summary>
/// Resource representing the Lambda Runtime API service emulator.
/// </summary>
/// <param name="name">Aspire resource name</param>
public class LambdaEmulatorResource(string name) : ExecutableResource(name, 
        "dotnet",
        Environment.CurrentDirectory
        )
{
    internal void AddCommandLineArguments(IList<object> arguments, LambdaEmulatorOptions? options)
    {
        arguments.Add("lambda-test-tool");
        arguments.Add("start");
        arguments.Add("--no-launch-window");

        if (options == null || options.ConfigStoragePath == null)
        {
            arguments.Add("--config-storage-path");
            arguments.Add(Path.Combine(Environment.CurrentDirectory, Constants.DefaultLambdaConfigStorage));
        }
        else if (options.ConfigStoragePath != string.Empty) // If set explicitly to empty string assume the user doesn't want local storage.
        {
            arguments.Add("--config-storage-path");
            arguments.Add(options.ConfigStoragePath);
        }
    }
}
