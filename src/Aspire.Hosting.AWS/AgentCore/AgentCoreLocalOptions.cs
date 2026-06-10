#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.AgentCore;

/// <summary>
/// Options for configuring the AgentCore local development experience.
/// </summary>
[Experimental(Constants.ASPIREAWSAGENTCORE001)]
public class AgentCoreLocalOptions
{
    /// <summary>
    /// When <c>true</c>, emulator logs (Runtime Emulator, Chat App, Memory Emulator)
    /// are forwarded to the agent's resource log stream in the Aspire Dashboard.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool IncludeEmulatorLogs { get; set; }
}
#endif
