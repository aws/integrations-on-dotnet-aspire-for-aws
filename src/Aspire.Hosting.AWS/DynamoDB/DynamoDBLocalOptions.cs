// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics;

namespace Aspire.Hosting.AWS.DynamoDB;

/// <summary>
/// Options that can be set for configuring the instance of DynamoDB local.
/// </summary>
[DebuggerDisplay("Type = {GetType().Name,nq}, Registry = {Registry}, Image = {Image}, Tag = {Tag}, LocalStorageDirectory = {LocalStorageDirectory}, SharedDb = {SharedDb}, DisableDynamoDBLocalTelemetry = {DisableDynamoDBLocalTelemetry}, DelayTransientStatuses = {DelayTransientStatuses}")]
public class DynamoDBLocalOptions
{
    /// <summary>
    /// The registry of the container image. THe default is public.ecr.aws.
    /// </summary>
    public string Registry { get; set; } = "public.ecr.aws";

    /// <summary>
    /// The container image to run for DynamoDB local. The default is aws-dynamodb-local/aws-dynamodb-local.
    /// </summary>
    public string Image { get; set; } = "aws-dynamodb-local/aws-dynamodb-local";

    /// <summary>
    /// The container image tag. The default is latest.
    /// </summary>
    public string Tag { get; set; } = "latest";

    /// <summary>
    /// If set to true DynamoDB local uses a single database file instead of separate files for each credential and Region.
    /// </summary>
    public bool SharedDb { get; set; }

    /// <summary>
    /// If set to true DynamoDB runs in memory instead of using a database file. DynamoDB local will run faster
    /// using InMemory mode but all data will be lost when the container ends and the data stored in DynamoDB
    /// local can not exceed the available memory for the container.
    /// </summary>
    public bool InMemory { get; set; }

    /// <summary>
    /// Directory on host machine to create the DynamoDB local database files. If this property is set the data
    /// written to DynamoDB local will persist between AppHost invocations.
    /// </summary>
    public string? LocalStorageDirectory { get; set; }

    /// <summary>
    /// If set to true disabled DynamoDB local's telemetry by setting the DDB_LOCAL_TELEMETRY environment variable
    /// </summary>
    public bool DisableDynamoDBLocalTelemetry { get; set; }

    /// <summary>
    /// If set to true causes DynamoDB local to introduce delays for certain operations. DynamoDB local can perform 
    /// some tasks almost instantaneously, such as create/update/delete operations on tables and indexes. However, the DynamoDB 
    /// service requires more time for these tasks. Setting this parameter helps DynamoDB running on your computer 
    /// simulate the behavior of the DynamoDB web service more closely.
    /// </summary>
    public bool DelayTransientStatuses { get; set; }

}
