// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Options that can be added to the Lambda emulator resource.
/// </summary>
public class LambdaEmulatorOptions
{
    /// <summary>
    /// By default Amazon.Lambda.TestTool will be updated/installed during AppHost startup. Amazon.Lambda.TestTool is 
    /// a .NET Tool that will be installed globally.
    /// 
    /// When DisableAutoInstall is set to true the auto installation is disabled.
    /// </summary>
    public bool DisableAutoInstall { get; set; } = false;

    /// <summary>
    /// Override the minimum version of Amazon.Lambda.TestTool that will be installed. If a newer version is already installed
    /// it will be used unless AllowDowngrade is set to true.
    /// </summary>
    public string? OverrideMinimumInstallVersion { get; set; } = null;

    /// <summary>
    /// If set to true, and a newer version of Amazon.Lambda.TestTool is already installed then the requested version, the installed version
    /// will be downgraded to the request version.
    /// </summary>
    public bool AllowDowngrade { get; set; } = false;

    /// <summary>
    /// The http port that the Lambda emulator will listen on. If not set, a random port will be used.
    /// </summary>
    public int? HttpPort { get; set; } = null;

    /// <summary>
    /// The https port that the Lambda emulator will listen on. If not set, a random port will be used.
    /// </summary>
    public int? HttpsPort { get; set; } = null;

    /// <summary>
    /// Directory for the Lambda Test Tool to save configuration information like saved requests. The default is ".aws-lambda-testtool" sub directory in the current directory.
    /// To disable the ability to save configuration set ConfigStoragePath to an empty string (i.e. string.Empty).
    /// </summary>
    public string? ConfigStoragePath { get; set; }

    /// <summary>
    /// By default both an HTTP and HTTPS endpoint will be created for the Lambda Emulator. The HTTP endpoint is required for running Lambda functions to poll and process events. 
    /// The web ui can be accessed through the HTTPS endpoint. Setting DisableHttpsEndpoint to true will disable the creation of the HTTPS endpoint.
    /// </summary>
    public bool DisableHttpsEndpoint { get; set; } = false;
}
