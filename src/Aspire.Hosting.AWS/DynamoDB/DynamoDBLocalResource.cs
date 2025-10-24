// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.DynamoDB;

/// <summary>
/// Represents a DynamoDB local resource. This is a dev only resources and will not be written to the project's manifest.
/// </summary>
public sealed class DynamoDBLocalResource(string name, DynamoDBLocalOptions options) : ContainerResource(name), IDynamoDBLocalResource
{
    internal const int DynamoDBInternalPort = 8000;
    internal const string InternalStorageMountPoint = "/storage";

    internal DynamoDBLocalOptions Options { get; } = options;

    /// <summary>
    /// Create the list of command line arguments that must be used when running the DynamoDB local container image.
    /// </summary>
    /// <returns></returns>
    internal string[] CreateContainerImageArguments()
    {
        var arguments = new List<string>
        {
            "-Djava.library.path=./DynamoDBLocal_lib",
            "-jar",
            "DynamoDBLocal.jar"
        };

        if (Options.SharedDb)
            arguments.Add("-sharedDb");

        if (Options.InMemory)
            arguments.Add("-inMemory");

        if (Options.DisableDynamoDBLocalTelemetry)
            arguments.Add("-disableTelemetry");

        if (!string.IsNullOrEmpty(Options.LocalStorageDirectory))
        {
            arguments.Add("-dbPath");
            arguments.Add(InternalStorageMountPoint);
        }

        if (Options.DelayTransientStatuses)
            arguments.Add("-delayTransientStatuses");

        return arguments.ToArray();
    }
}
