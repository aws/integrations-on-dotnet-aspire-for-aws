// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Utils;

internal static class AspireUtilities
{
    internal static bool IsRunningInDebugger => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEBUG_SESSION_PORT"));
}
