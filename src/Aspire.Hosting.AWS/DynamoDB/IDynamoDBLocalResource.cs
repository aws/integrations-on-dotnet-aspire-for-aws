// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.DynamoDB;

/// <summary>
/// Represents a DynamoDB local resource. This is a dev only resources and will not be written to the project's manifest.
/// </summary>
public interface IDynamoDBLocalResource : IResourceWithEnvironment, IResourceWithEndpoints
{

}