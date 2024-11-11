// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.DynamoDB;

/// <summary>
/// Options that can be set for configuring the instance of DynamoDB Local.
/// </summary>
public class DynamoDBLocalOptions
{
    /// <summary>
    /// If set to true disabled DynamoDB Local's telemetry by setting the DDB_LOCAL_TELEMETRY environment variable
    /// </summary>
    public bool DisableDynamoDBLocalTelemetry { get; set; } = false;
}
