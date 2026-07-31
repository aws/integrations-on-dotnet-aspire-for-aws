// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.DynamoDB;
using Aspire.Hosting.AWS.Lambda;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Aspire resource representing a Python Lambda function, run locally via the
/// AWS Lambda Runtime Interface Client (<c>awslambdaric</c>).
/// </summary>
/// <remarks>
/// The resource launches the Python executable found in the <c>.venv</c> virtual environment
/// inside <paramref name="appDirectory"/>. If no virtual environment is found it falls back
/// to the system <c>python3</c> / <c>python</c> executable.
/// </remarks>
public class PythonLambdaFunctionResource(string name, string executablePath, string appDirectory)
    : ExecutableResource(name, executablePath, appDirectory), ILambdaFunctionResource
{
    /// <summary>
    /// The directory containing the Python Lambda function source code.
    /// </summary>
    public string AppDirectory { get; } = appDirectory;

    /// <summary>
    /// Reference to a DynamoDB Local instance, used by the DynamoDB Streams event-source
    /// poller to redirect DynamoDB and Streams calls to the local container.
    /// </summary>
    internal IResourceBuilder<DynamoDBLocalResource>? DynamoDBLocalInstance { get; set; }

    IResourceBuilder<DynamoDBLocalResource>? ILambdaFunctionResource.DynamoDBLocalInstance
    {
        get => DynamoDBLocalInstance;
        set => DynamoDBLocalInstance = value;
    }
}
