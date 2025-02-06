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
    internal void AddCommandLineArguments(IList<object> arguments)
    {
        arguments.Add("lambda-test-tool");
        arguments.Add("--no-launch-window");
    }
}
