#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.AgentCore;
using Aspire.Hosting.Publishing;
using AWS.AgentCore.Testing;
using AWS.AgentCore.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding AgentCore local development resources.
/// All emulators run as embedded in-process Kestrel servers — no Docker or separate processes required.
/// </summary>
public static class AgentCoreResourceBuilderExtensions
{
    /// <summary>
    /// Registers an AgentCore agent with a dedicated runtime emulator and chat app.
    /// Returns the agent's <see cref="IResourceBuilder{ProjectResource}"/>.
    /// Use Aspire's <c>.WithReference(agent)</c> from another project to inject the runtime
    /// emulator endpoint as the <c>AWS_ENDPOINT_URL_BEDROCK_AGENTCORE</c> environment variable.
    /// </summary>
    /// <typeparam name="TProject">The agent project type.</typeparam>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">Optional configuration for the local development experience.</param>
    /// <returns>The agent's resource builder for further configuration.</returns>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<ProjectResource> AddAgentCoreRuntime<TProject>(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        AgentCoreLocalOptions? options = null)
        where TProject : IProjectMetadata, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        options ??= new AgentCoreLocalOptions();

        var projectName = name;

        var agentApp = builder.AddProject<TProject>(projectName, o => o.ExcludeLaunchProfile = true)
            .WithHttpEndpoint(name: "http")
            .WithEnvironment("AWS_AGENTCORE_ASPIRE_MANAGED", "true")
            // Bedrock AgentCore only runs arm64 container images, so the published image must be
            // built for linux/arm64 regardless of the host architecture. This affects publish only;
            // local development runs the project directly without a container.
            .WithContainerBuildOptions(context => context.TargetPlatform = ContainerTargetPlatform.LinuxArm64);

        // Suppress default endpoint URLs — we add our own with display names
        agentApp.WithUrls(context =>
        {
            context.Urls.RemoveAll(u =>
                u.Endpoint is not null &&
                u.DisplayText != "Chat" &&
                u.DisplayText != "Runtime Emulator" &&
                u.DisplayText != "Agent Instance");
        });

        var annotation = new AgentCoreRuntimeAnnotation
        {
            IncludeEmulatorLogs = options.IncludeEmulatorLogs,
            RuntimeEmulatorPort = options.RuntimeEmulatorPort ?? 0,
            ChatAppPort = options.ChatAppPort ?? 0,
            MemoryEmulatorPort = options.MemoryEmulatorPort ?? 0
        };
        agentApp.Resource.Annotations.Add(annotation);

        // Start emulators before the agent resource starts.
        // This guarantees ports are known before env var callbacks fire.
        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(
            agentApp.Resource,
            async (@event, ct) =>
            {
                try
                {
                    var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();

                    var agentEndpoint = agentApp.Resource.Annotations
                        .OfType<EndpointAnnotation>()
                        .First(e => e.Name == "http");

                    var agentEndpointUrl = agentEndpoint.AllocatedEndpoint?.UriString
                        ?? throw new InvalidOperationException(
                            $"The 'http' endpoint for resource '{projectName}' has not been allocated.");

                    ILoggerProvider? loggerProvider = null;
                    if (annotation.IncludeEmulatorLogs)
                    {
                        loggerProvider = new AspireLoggerProvider(
                            loggerService.GetLogger(agentApp.Resource));
                    }

                    // Start runtime emulator (port 0 = OS-assigned)
                    var runtimeApp = RuntimeEmulatorServer.Create(agentEndpointUrl, port: annotation.RuntimeEmulatorPort, loggerProvider: loggerProvider);
                    await runtimeApp.StartAsync(ct);
                    annotation.EmulatorServers.Add(runtimeApp);
                    annotation.RuntimeEmulatorPort = GetBoundPort(runtimeApp);

                    var runtimeUrl = $"http://localhost:{annotation.RuntimeEmulatorPort}";

                    // Start chat app (port 0 = OS-assigned)
                    var chatApp = ChatAppServer.Create(runtimeUrl, port: annotation.ChatAppPort, streaming: annotation.IsStreaming, agentName: projectName, loggerProvider: loggerProvider);
                    await chatApp.StartAsync(ct);
                    annotation.EmulatorServers.Add(chatApp);
                    annotation.ChatAppPort = GetBoundPort(chatApp);

                    // Start memory emulator if configured
                    if (annotation.HasMemory)
                    {
                        var memoryApp = MemoryEmulatorServer.Create(port: annotation.MemoryEmulatorPort, loggerProvider: loggerProvider);
                        await memoryApp.StartAsync(ct);
                        annotation.EmulatorServers.Add(memoryApp);
                        annotation.MemoryEmulatorPort = GetBoundPort(memoryApp);
                    }

                    // Add URLs (order: Chat, Runtime Emulator, Agent Instance)
                    agentApp.Resource.Annotations.Add(new ResourceUrlAnnotation
                    {
                        Url = $"http://localhost:{annotation.ChatAppPort}",
                        DisplayText = "Chat"
                    });
                    agentApp.Resource.Annotations.Add(new ResourceUrlAnnotation
                    {
                        Url = runtimeUrl,
                        DisplayText = "Runtime Emulator"
                    });
                    agentApp.Resource.Annotations.Add(new ResourceUrlAnnotation
                    {
                        Url = agentEndpointUrl,
                        DisplayText = "Agent Instance"
                    });

                    annotation.EmulatorStarted.TrySetResult();
                }
                catch (Exception ex)
                {
                    await annotation.StopEmulatorsAsync();
                    annotation.EmulatorStarted.TrySetException(ex);
                    throw;
                }
            });

        builder.Eventing.Subscribe<ResourceStoppedEvent>(
            agentApp.Resource,
            async (@event, ct) =>
            {
                await annotation.StopEmulatorsAsync();
            });

        // Wire AWS_ENDPOINT_URL_BEDROCK_AGENTCORE on consumers that .WithReference(agent).
        // Hooks Aspire's stock WithReference relationship instead of shadowing the overload,
        // so users keep using the standard Aspire idiom without overload-resolution surprises.
        EnsureReferenceHook(builder);

        return agentApp;
    }

    /// <summary>
    /// Configures the chat app to use streaming (SSE) mode for this agent.
    /// </summary>
    /// <param name="agentApp">The agent resource builder.</param>
    /// <returns>The resource builder for further chaining.</returns>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<ProjectResource> WithAgentCoreStreaming(
        this IResourceBuilder<ProjectResource> agentApp)
    {
        var annotation = agentApp.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "WithAgentCoreStreaming can only be called on an AgentCore runtime resource. " +
                "Use AddAgentCoreRuntime<T>() to create it.");

        annotation.IsStreaming = true;

        return agentApp;
    }

    /// <summary>
    /// Enables AgentCore memory for the agent application.
    /// <para>
    /// During local development this adds an embedded memory emulator and sets the
    /// <c>AWS_AGENTCORE_MEMORY_ID</c> and <c>AWS_AGENTCORE_SERVICE_ENDPOINT</c> environment variables on
    /// provisioned and <c>AWS_AGENTCORE_MEMORY_ID</c> is pointed at it. Deployment memory creation can be
    /// overridden with <see cref="Aspire.Hosting.AWS.Deployment.PublishAgentCoreRuntimeConfig.CreateMemory"/>.
    /// </para>
    /// </summary>
    /// <param name="agentApp">The agent resource builder.</param>
    /// <returns>The resource builder for further chaining.</returns>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<ProjectResource> WithAgentCoreMemory(
        this IResourceBuilder<ProjectResource> agentApp)
    {
        var annotation = agentApp.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "WithAgentCoreMemory can only be called on an AgentCore runtime resource. " +
                "Use AddAgentCoreRuntime<T>() to create it.");

        annotation.HasMemory = true;

        agentApp.WithEnvironment(Constants.AgentCoreMemoryIdEnvironmentVariable, "localdev-memory");

        agentApp.WithEnvironment(async context =>
        {
            if (context.ExecutionContext.IsPublishMode)
            {
                return;
            }

            await annotation.EmulatorStarted.Task;
            context.EnvironmentVariables["AWS_AGENTCORE_SERVICE_ENDPOINT"] =
                $"http://localhost:{annotation.MemoryEmulatorPort}";
        });

        return agentApp;
    }

    private static int GetBoundPort(Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var address = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("Emulator server did not bind to any address after StartAsync.");

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Emulator server bound to an invalid address: '{address}'.");
        }

        return uri.Port;
    }

    /// <summary>
    /// DI marker singleton used to detect whether the reference hook has already been
    /// subscribed for the current app builder. The instance is never resolved — only its
    /// presence in <see cref="IServiceCollection"/> is checked.
    /// </summary>
    private sealed class AgentCoreReferenceHookMarker;

    /// <summary>
    /// Subscribes a one-time, app-wide <see cref="BeforeStartEvent"/> handler that wires
    /// <c>AWS_ENDPOINT_URL_BEDROCK_AGENTCORE</c> onto any resource that calls Aspire's
    /// stock <c>WithReference(agent)</c> against an AgentCore runtime.
    ///
    /// <para>How it works:</para>
    /// <list type="number">
    /// <item><description>
    /// Aspire's <c>WithReference</c> writes a <see cref="ResourceRelationshipAnnotation"/>
    /// with <c>Type == "Reference"</c> on the consumer, pointing at the source resource —
    /// for every <c>WithReference</c> overload.
    /// </description></item>
    /// <item><description>
    /// On <see cref="BeforeStartEvent"/> the handler walks every
    /// <see cref="IResourceWithEnvironment"/> in the model and inspects its relationship
    /// annotations. References whose target carries an <see cref="AgentCoreRuntimeAnnotation"/>
    /// (i.e. were created by <c>AddAgentCoreRuntime</c>) are treated as AgentCore references.
    /// Multiple annotations pointing at the same source are deduped.
    /// </description></item>
    /// <item><description>
    /// If a consumer references two or more <i>distinct</i> AgentCore runtimes the handler
    /// throws — <c>AWS_ENDPOINT_URL_BEDROCK_AGENTCORE</c> is a single-valued AWS SDK
    /// endpoint override and cannot point to multiple runtimes simultaneously.
    /// </description></item>
    /// <item><description>
    /// For the resolved single agent, an <see cref="EnvironmentCallbackAnnotation"/> is added
    /// to the consumer. The callback awaits the agent's
    /// <see cref="AgentCoreRuntimeAnnotation.EmulatorStarted"/> task — set when the agent's
    /// <see cref="BeforeResourceStartedEvent"/> handler has bound the runtime emulator port —
    /// and writes <c>AWS_ENDPOINT_URL_BEDROCK_AGENTCORE=http://localhost:{port}</c>. Publish
    /// mode is skipped because the emulator only runs locally.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// Subscription is guarded by <see cref="AgentCoreReferenceHookMarker"/> in
    /// <see cref="IServiceCollection"/>: every <c>AddAgentCoreRuntime</c> call invokes this
    /// method, but the handler walks the entire model, so subscribing N times for N agents
    /// would inject the env var N times per consumer.
    /// </para>
    /// </summary>
    private static void EnsureReferenceHook(IDistributedApplicationBuilder builder)
    {
        if (builder.Services.Any(s => s.ServiceType == typeof(AgentCoreReferenceHookMarker)))
        {
            return;
        }
        builder.Services.AddSingleton<AgentCoreReferenceHookMarker>();

        builder.Eventing.Subscribe<BeforeStartEvent>(static (@event, ct) =>
        {
            foreach (var consumer in @event.Model.Resources)
            {
                // Only resources that accept env vars can receive AWS_ENDPOINT_URL_BEDROCK_AGENTCORE.
                if (consumer is not IResourceWithEnvironment)
                {
                    continue;
                }

                // Pick out relationship annotations whose target is an AgentCore runtime.
                // Aspire writes a ResourceRelationshipAnnotation(Type = "Reference") for every
                // WithReference call regardless of overload, so this catches all reference shapes.
                // Dedupe by source resource: two WithReference(sameAgent) calls produce two
                // relationship annotations, but they're the same logical reference.
                var agentRefs = consumer.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Where(r => r.Type == "Reference")
                    .Select(r => (Source: r.Resource, Annotation: r.Resource.Annotations.OfType<AgentCoreRuntimeAnnotation>().FirstOrDefault()))
                    .Where(x => x.Annotation is not null)
                    .GroupBy(x => x.Source)
                    .Select(g => g.First())
                    .ToList();

                if (agentRefs.Count == 0)
                {
                    continue;
                }

                // AWS_ENDPOINT_URL_BEDROCK_AGENTCORE is single-valued — referencing two
                // distinct AgentCore runtimes from the same consumer is unsupported.
                if (agentRefs.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Resource '{consumer.Name}' has WithReference calls to multiple AgentCore runtimes " +
                        $"({string.Join(", ", agentRefs.Select(r => r.Source.Name))}). " +
                        "Only one AgentCore runtime reference is supported per resource because the " +
                        "AWS_ENDPOINT_URL_BEDROCK_AGENTCORE environment variable can only point to a single endpoint.");
                }

                // Defer the env var value until the agent's emulator has actually bound a port.
                // EmulatorStarted is completed by the agent's own BeforeResourceStartedEvent handler.
                var ann = agentRefs[0].Annotation!;
                var agentName = agentRefs[0].Source.Name;
                var agentRuntimeArnKey =
                    $"{Constants.DefaultConfigSection}:{agentName}:{Constants.AgentRuntimeArnOutputName}".ToEnvironmentVariables();
                consumer.Annotations.Add(new EnvironmentCallbackAnnotation(async ctx =>
                {
                    if (ctx.ExecutionContext.IsPublishMode)
                    {
                        return;
                    }

                    await ann.EmulatorStarted.Task;
                    ctx.EnvironmentVariables["AWS_ENDPOINT_URL_BEDROCK_AGENTCORE"] =
                        $"http://localhost:{ann.RuntimeEmulatorPort}";

                    // Mirror the deployment convention so app code reads the same IConfiguration key
                    // in both modes. The emulator ignores the ARN, so a placeholder is sufficient locally;
                    // at deploy time the CDK stack sets this to the real AWS::BedrockAgentCore::Runtime ARN.
                    ctx.EnvironmentVariables[agentRuntimeArnKey] = "local-agent";
                }));
            }

            return Task.CompletedTask;
        });
    }
}
#endif
