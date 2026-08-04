// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.DynamoDB;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Marker interface shared by .NET and Python Lambda function resources.
/// Allows generic event-source and API Gateway extensions to work with both resource types.
/// </summary>
internal interface ILambdaFunctionResource : IResource
{
    /// <summary>
    /// Reference to a DynamoDB Local instance so that DynamoDB Streams event-source polling
    /// can be directed to it instead of the real AWS endpoint.
    /// </summary>
    IResourceBuilder<DynamoDBLocalResource>? DynamoDBLocalInstance { get; set; }
}
