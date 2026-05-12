// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Optional settings for configuring a DynamoDB Streams event source for a Lambda function.
/// </summary>
public class DynamoDBStreamsEventSourceOptions
{
    /// <summary>
    /// Optional unique resource name for the DynamoDBStreamsEventSourceResource. When adding multiple DynamoDB tables to the same Lambda,
    /// set this to avoid duplicate resource name errors. If not set, the default pattern uses the Lambda and table names.
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// The batch size to read from the DynamoDB stream and send to the Lambda function.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// The polling interval in milliseconds between stream reads when no records are found.
    /// If not set, uses the Lambda test tool default (currently 1000ms).
    /// </summary>
    public int? PollingIntervalMs { get; set; }
}
