// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS;
internal static class Constants
{

    /// <summary>
    /// Error state for Aspire resource dashboard
    /// </summary>
    public const string ResourceStateFailedToStart = "FailedToStart";

    /// <summary>
    /// In progress state for Aspire resource dashboard
    /// </summary>
    public const string ResourceStateStarting = "Starting";

    /// <summary>
    /// Success state for Aspire resource dashboard
    /// </summary>
    public const string ResourceStateRunning = "Running";

    /// <summary>
    /// Default Configuration Section
    /// </summary>
    public const string DefaultConfigSection = "AWS:Resources";

    internal const string IsAspireHostedEnvVariable = "ASPIRE_HOSTED";

    internal const string LambdaPreviewMessage = "Local Lambda development feature is still in active development. Check out the following GitHub issue for status: https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17";
    
    /// <summary>
    /// The launch settings profile name prefix
    /// </summary>
    internal const string LaunchSettingsNodePrefix = "Aspire_";
    
    /// <summary>
    /// The launch settings file name
    /// </summary>
    internal const string LaunchSettingsFile = "launchSettings.json";
    
    /// <summary>
    /// The version of RuntimeSupport used in the executable wrapper project
    /// </summary>
    internal const string RuntimeSupportPackageVersion = "1.13.0";
    
    /// <summary>
    /// The default version of Amazon.Lambda.TestTool that will be automatically installed
    /// </summary>
    internal const string DefaultLambdaTestToolVersion = "0.10.1";
}
