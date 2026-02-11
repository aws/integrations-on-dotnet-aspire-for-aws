// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Annotation for the metadata of a Lambda runtime emulator resource
/// </summary>
/// <param name="lambdaRuntimeEndpoint"></param>
internal class LambdaEmulatorAnnotation(EndpointReference lambdaRuntimeEndpoint) : IResourceAnnotation
{
    /// <summary> 
    /// The HTTP endpoint for the Lambda runtime api.
    /// </summary>
    public EndpointReference LambdaRuntimeEndpoint { get; init; } = lambdaRuntimeEndpoint;

    /// <summary>
    /// By default Amazon.Lambda.TestTool will be updated/installed during AppHost startup. Amazon.Lambda.TestTool is 
    /// a .NET Tool that will be installed globally.
    /// 
    /// When DisableAutoInstall is set to true the auto installation is disabled.
    /// </summary>
    public bool DisableAutoInstall { get; set; }

    /// <summary>
    /// Override the minimum version of Amazon.Lambda.TestTool that will be installed. If a newer version is already installed
    /// it will be used unless AllowDowngrade is set to true.
    /// </summary>
    public string? OverrideMinimumInstallVersion { get; set; }

    /// <summary>
    /// If set to true and a newer version of the Amazon.Lambda.TestTool is installed then expected the installed version will be downgraded
    /// to match the expected version.
    /// </summary>
    public bool AllowDowngrade { get; set; }
}
