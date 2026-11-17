// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Collects API Gateway emulator Lambda routes so multiple <c>WithReference</c> calls
/// for the same function serialize as a JSON array instead of overwriting one env var.
/// </summary>
internal sealed class ApiGatewayEmulatorRoutesAnnotation : IResourceAnnotation
{
    public List<ApiGatewayEmulatorLambdaRoute> LambdaRoutes { get; } = [];
    public bool EnvironmentCallbackRegistered { get; set; }
}

internal sealed class ApiGatewayEmulatorLambdaRoute
{
    public required string LambdaResourceName { get; init; }
    public required Method HttpMethod { get; init; }
    public required string Path { get; init; }
    public required Func<string> Endpoint { get; init; }
}
