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