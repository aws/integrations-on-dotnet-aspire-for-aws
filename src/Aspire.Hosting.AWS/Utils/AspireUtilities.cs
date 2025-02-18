// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Utils;

public static class AspireUtilities
{
    public static bool IsRunningInDebugger => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEBUG_SESSION_PORT"));
}
