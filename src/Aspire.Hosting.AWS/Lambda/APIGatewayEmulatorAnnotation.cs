// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Annotation for the metadata of the API Gateway emulator
/// </summary>
/// <param name="apiGatewayType"></param>
internal class APIGatewayEmulatorAnnotation(APIGatewayType apiGatewayType) : IResourceAnnotation
{
    internal APIGatewayType ApiGatewayType { get; set; } = apiGatewayType;
}
