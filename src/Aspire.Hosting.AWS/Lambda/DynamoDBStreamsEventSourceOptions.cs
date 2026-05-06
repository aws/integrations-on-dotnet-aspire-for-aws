// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// The position in the stream where reading begins.
/// </summary>
public enum DynamoDBStreamsIteratorType
{
    /// <summary>
    /// Start reading just after the most recent stream record, so you only process new records.
    /// </summary>
    Latest,

    /// <summary>
    /// Start reading at the last untrimmed record in the shard, processing all available records.
    /// </summary>
    TrimHorizon
}

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
    /// The position in the stream where reading begins. Default is <see cref="DynamoDBStreamsIteratorType.Latest"/>.
    /// </summary>
    public DynamoDBStreamsIteratorType? ShardIteratorType { get; set; }

    /// <summary>
    /// The polling interval in milliseconds between stream reads when no records are found.
    /// Default is 1000.
    /// </summary>
    public int? PollingIntervalMs { get; set; }
}
