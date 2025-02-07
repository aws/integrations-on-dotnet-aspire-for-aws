// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using k8s.KubeConfigModels;

namespace Aspire.Hosting.AWS.Lambda;


/// <summary>
/// Resource representing the Amazon API Gateway emulator.
/// </summary>
/// <param name="name">Aspire resource name</param>
public class APIGatewayEmulatorResource(string name, APIGatewayType apiGatewayType) : ExecutableResource(name, 
        "dotnet",
        Environment.CurrentDirectory
        )
{
    internal void AddCommandLineArguments(IList<object> arguments)
    {
        arguments.Add("lambda-test-tool");
        arguments.Add("start");
        arguments.Add("--no-launch-window");

        arguments.Add("--api-gateway-emulator-mode");
        arguments.Add(apiGatewayType.ToString());
    }
}
