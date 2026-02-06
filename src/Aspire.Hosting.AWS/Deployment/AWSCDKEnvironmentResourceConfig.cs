// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// Container for the optional configuration settings for the <see cref="AWSCDKEnvironmentResource"/>.
/// </summary>
public class AWSCDKEnvironmentResourceConfig
{
    /// <summary>
    /// The AWS SDK configuration to use when publishing an deploying to AWS. If not set
    /// the region and credential information will be inferred from the environment.
    /// </summary>
    public IAWSSDKConfig? AWSSDKConfig { get; init; }
    
    /// <summary>
    /// Set to true to disable the correction if publishing on Windows and deploying
    /// on a non Windows platform.
    /// <para>
    /// When performing an Aspire publish a CDK synth is performed. Part of the synth process
    /// creates an assets manifest with commands to load tarball assets.
    /// CDK natively uses commands that do not work on Windows. The Aspire integration
    /// detects if publish is happening on Windows. If so replace the commands in the
    /// asset manifest to be compatible for Windows.
    /// </para>
    /// </summary>
    public bool DisablePlatformCorrection { get; init; }

    /// <summary>
    /// For testing only:
    /// 
    /// When we need to fork the process to get CDK context information we determine the AppHost by assuming
    /// it is the entry assembly. This is how a customer would use our Aspire integration. However,
    /// in the integ tests that kicks off the publish the testhost.dll is the entry assembly.
    /// This property allows overriding the assembly name in the integ tests so we
    /// can still get the CDK context.
    /// </summary>
    internal string? OverrideAppHostAssemblyName { get; init; }
}
