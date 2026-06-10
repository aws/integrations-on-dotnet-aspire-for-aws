#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Microsoft.AspNetCore.Builder;

namespace Aspire.Hosting.AWS.AgentCore;

/// <summary>
/// Annotation attached to an agent's <see cref="ProjectResource"/> by
/// <see cref="AgentCoreResourceBuilderExtensions.AddAgentCoreRuntime{TProject}"/>.
/// Stores the actual bound ports for the embedded emulators (resolved after startup).
/// </summary>
internal class AgentCoreRuntimeAnnotation : IResourceAnnotation
{
    public int RuntimePort { get; set; }
    public int ChatAppPort { get; set; }
    public int MemoryPort { get; set; }
    public bool IsStreaming { get; set; }
    public bool HasMemory { get; set; }
    public bool IncludeEmulatorLogs { get; set; }
    public TaskCompletionSource EmulatorStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal List<WebApplication> EmulatorServers { get; } = [];

    internal async Task StopEmulatorsAsync()
    {
        foreach (var server in EmulatorServers)
        {
            try
            {
                await server.StopAsync();
            }
            catch
            {
                // Best-effort shutdown
            }
        }
    }
}

/// <summary>
/// Marker annotation to track that a project already has an AgentCore runtime reference.
/// Used to enforce the single-reference constraint.
/// </summary>
internal class AgentCoreReferenceAnnotation : IResourceAnnotation;
#endif
