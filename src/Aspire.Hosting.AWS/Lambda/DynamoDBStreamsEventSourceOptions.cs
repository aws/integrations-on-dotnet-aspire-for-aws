// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Optional settings for configuring a DynamoDB Streams event source for a Lambda function.
/// </summary>
public class DynamoDBStreamsEventSourceOptions
{
    /// <summary>
    /// The batch size to read from the DynamoDB stream and send to the Lambda function.
    /// </summary>
    public int? BatchSize { get; set; }
}
