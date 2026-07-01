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

    /// <summary>
    /// The output name appended to an AgentCore runtime's config section when a resource references it.
    /// The full key is <c>AWS:Resources:{agentName}:AgentRuntimeArn</c>. Shared between the local
    /// development reference hook and the deployment publish target so the convention can never drift.
    /// </summary>
    internal const string AgentRuntimeArnOutputName = "AgentRuntimeArn";

    /// <summary>
    /// The environment variable the agent reads to discover its AgentCore memory id. Shared between the
    /// local development emulator wiring and the deployment publish target so the name can never drift.
    /// </summary>
    internal const string AgentCoreMemoryIdEnvironmentVariable = "AWS_AGENTCORE_MEMORY_ID";

    internal const string IsAspireHostedEnvVariable = "ASPIRE_HOSTED";
    
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
    internal const string RuntimeSupportPackageVersion = "2.0.0";
    
    /// <summary>
    /// The default version of Amazon.Lambda.TestTool that will be automatically installed
    /// </summary>
    internal const string DefaultLambdaTestToolVersion = "0.14.1";

    /// <summary>
    /// The default directory the Lambda Test Tool will be configured for storing configuration information like saved requests.
    /// </summary>
    internal const string DefaultLambdaConfigStorage = ".aws-lambda-testtool";

    /// <summary>
    /// Diagnostic ID for Aspire publishing with AWS indicating the feature is experimental.
    /// </summary>
    internal const string ASPIREAWSPUBLISHERS001 = "ASPIREAWSPUBLISHERS001";

    /// <summary>
    /// Diagnostic ID for AgentCore local development extensions indicating the feature is experimental.
    /// </summary>
    internal const string ASPIREAWSAGENTCORE001 = "ASPIREAWSAGENTCORE001";
}
