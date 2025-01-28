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

    internal const string LambdaPreviewMessage = "Local Lambda development feature is still in active development. Check out the following GitHub issue for status: https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17";
}
