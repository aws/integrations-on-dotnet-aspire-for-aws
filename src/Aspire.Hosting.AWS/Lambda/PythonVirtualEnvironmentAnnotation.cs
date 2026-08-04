// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Annotation that carries virtual-environment configuration for a
/// <see cref="PythonLambdaFunctionResource"/>.
/// </summary>
internal sealed class PythonVirtualEnvironmentAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Absolute path to the virtual environment directory.
    /// </summary>
    public string VirtualEnvironmentPath { get; }

    /// <summary>
    /// When <c>true</c>, the virtual environment is created via <c>python -m venv</c>
    /// and <c>pip install -r requirements.txt</c> is run before the Lambda process starts.
    /// </summary>
    public bool CreateIfNotExists { get; }

    /// <summary>
    /// Python executable used to create the virtual environment when it does not exist.
    /// Examples: <c>python3</c>, <c>python</c>, or an absolute interpreter path such as
    /// <c>/opt/homebrew/bin/python3.12</c>.
    /// </summary>
    public string PythonExecutablePath { get; }

    /// <summary>
    /// The resource builder associated with the Python Lambda function, used by the
    /// before-start handler to call <c>WithCommand</c> after the venv is created.
    /// </summary>
    public IResourceBuilder<PythonLambdaFunctionResource>? ResourceBuilder { get; set; }

    public PythonVirtualEnvironmentAnnotation(string virtualEnvironmentPath, bool createIfNotExists, string pythonExecutablePath)
    {
        VirtualEnvironmentPath = virtualEnvironmentPath;
        CreateIfNotExists = createIfNotExists;
        PythonExecutablePath = pythonExecutablePath;
    }
}
