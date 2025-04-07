// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Optional settings for configuring an SQS event source for a Lambda function.
/// </summary>
public class SQSEventSourceOptions
{    
     /// <summary>
     /// The batch size to read and send to Lambda function.
     /// SQS will return with less then batch size if there are not enough messages in the queue.
     /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// If true the messages read from the queue will not be deleted after being processed.
    /// </summary>
    public bool? DisableMessageDelete { get; set; }

    /// <summary>
    /// The visibility timeout used for messages read from the queue. This is the length the message will not be visible to be read
    /// again once it is returned in the receive call.
    /// </summary>
    public int? VisibilityTimeout { get; set; }
}
