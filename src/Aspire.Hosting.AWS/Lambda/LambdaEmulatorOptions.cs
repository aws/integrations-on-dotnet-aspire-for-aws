// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Options that can be added the Lambda emulator resource.
/// </summary>
public class LambdaEmulatorOptions
{
    /// <summary>
    /// If set to true, Amazon.Lambda.TestTool will updated/installed during AppHost startup. Amazon.Lambda.TestTool is 
    /// a .NET Tool that will be installed globally.
    /// </summary>
    public bool DisableAutoInstall { get; set; }

    /// <summary>
    /// Override the minimum version of Amazon.Lambda.TestTool that will be installed. If a newer version is already installed
    /// it will be used unless AllowDowngrade is set to true.
    /// </summary>
    public string? OverrideMinimumInstallVersion { get; set; }

    /// <summary>
    /// If set to true, and a newer version of Amazon.Lambda.TestTool is already installed then the requested version, the installed version
    /// will be downgraded to the request version.
    /// </summary>
    public bool AllowDowngrade { get; set; }
}
