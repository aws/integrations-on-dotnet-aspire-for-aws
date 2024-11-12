// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics;

namespace Aspire.Hosting.AWS.DynamoDB;

/// <summary>
/// Options that can be set for configuring the instance of DynamoDB Local.
/// </summary>
[DebuggerDisplay("Type = {GetType().Name,nq}, Image = {Image}, Tag = {Tag}, DisableDynamoDBLocalTelemetry = {DisableDynamoDBLocalTelemetry}")]
public class DynamoDBLocalOptions
{
    /// <summary>
    /// The registry of the container image
    /// </summary>
    public string Registry { get; set; } = "public.ecr.aws";

    /// <summary>
    /// The container image to run for DynamoDB Local. The default is public.ecr.aws/aws-dynamodb-local/aws-dynamodb-local.
    /// </summary>
    public string Image { get; set; } = "aws-dynamodb-local/aws-dynamodb-local";

    /// <summary>
    /// The container image tag. The default is latest.
    /// </summary>
    public string Tag { get; set; } = "latest";

    /// <summary>
    /// If set to true disabled DynamoDB Local's telemetry by setting the DDB_LOCAL_TELEMETRY environment variable
    /// </summary>
    public bool DisableDynamoDBLocalTelemetry { get; set; } = false;
}
