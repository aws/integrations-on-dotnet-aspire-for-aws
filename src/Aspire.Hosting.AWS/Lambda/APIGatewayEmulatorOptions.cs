// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Options that can be added to the API Gateway emulator resource.
/// </summary>
public class APIGatewayEmulatorOptions
{
    /// <summary>
    /// The port that the API Gateway emulator will listen on. If not set, a random port will be used.
    /// </summary>
    public int? Port { get; set; } = null;
}
