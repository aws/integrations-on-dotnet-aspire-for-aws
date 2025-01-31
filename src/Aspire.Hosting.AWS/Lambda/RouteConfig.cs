// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Text.Json.Serialization;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Class representing the config for a Lambda function that the API Gateway emulator uses to figure out what method to call.
/// This class is populated AWS Aspire code and serialied to JSON and saved as environment variable to the API Gatway
/// emulator as part of the emulator's config.
/// </summary>
internal class RouteConfig
{
    internal RouteConfig(string lambdaResourceName, string endpoint, Method httpMethod, string path)
    {
        LambdaResourceName = lambdaResourceName;
        Endpoint = endpoint;
        HttpMethod = httpMethod;
        Path = path;
    }

    public string LambdaResourceName { get; init; }

    public string Endpoint { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Method HttpMethod { get; init; }

    public string Path { get; init; }
}
