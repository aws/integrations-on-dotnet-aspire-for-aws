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

    /// <summary>
    /// Optional port for the Runtime Emulator endpoint.
    /// When <c>null</c>, a port is dynamically assigned.
    /// </summary>
    public int? RuntimeEmulatorPort { get; set; }

    /// <summary>
    /// Optional port for the Chat App endpoint.
    /// When <c>null</c>, a port is dynamically assigned.
    /// </summary>
    public int? ChatAppPort { get; set; }

    /// <summary>
    /// Optional port for the Memory Emulator endpoint.
    /// When <c>null</c>, a port is dynamically assigned.
    /// </summary>
    public int? MemoryEmulatorPort { get; set; }
}
#endif
