// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// The type of of API Gateway to configure the emulator for.
/// </summary>
public enum APIGatewayType { Rest, HttpV1, HttpV2 }

/// <summary>
/// The HTTP method a Lambda function should be called for.
/// </summary>
public enum Method { Any, Get, Post, Put, Delete, Patch, Head, Options}

/// <summary>
/// API Gateway emulator integration. Wire values match <c>Amazon.Lambda.TestTool</c>
/// <c>ApiGatewayIntegrationType</c> (JSON strings <c>Lambda</c> / <c>Http</c>).
/// </summary>
public enum ApiGatewayIntegrationType
{
    /// <summary>
    /// Translate the HTTP request into an API Gateway event and invoke a local Lambda.
    /// </summary>
    Lambda,

    /// <summary>
    /// Reverse-proxy the HTTP request to the route endpoint without Lambda event wrapping.
    /// </summary>
    Http
}
