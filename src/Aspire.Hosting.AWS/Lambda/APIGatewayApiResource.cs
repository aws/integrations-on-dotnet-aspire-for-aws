// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;


/// <summary>
/// Resource representing the Amazon API Gateway emulator.
/// </summary>
/// <param name="name">Aspire resource name</param>
public class APIGatewayApiResource(string name) : ExecutableResource(name, 
        "dotnet-lambda-test-tool",
        Environment.CurrentDirectory
        )
{
}
