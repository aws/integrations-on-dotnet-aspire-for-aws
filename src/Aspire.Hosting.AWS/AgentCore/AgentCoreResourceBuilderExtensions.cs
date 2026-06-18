#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.AgentCore;
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
    /// Use <c>.WithReference(agent)</c> from another project to inject the runtime
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
            .WithEnvironment("AWS_AGENTCORE_ASPIRE_MANAGED", "true");

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

        return agentApp;
    }

    /// <summary>
    /// Wires a project to an AgentCore agent by injecting the runtime emulator endpoint
    /// as the <c>AWS_ENDPOINT_URL_BEDROCK_AGENTCORE</c> environment variable.
    /// The AWS SDK picks this up automatically for endpoint routing — no manual
    /// <c>ServiceURL</c> configuration is needed in the consuming project.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The project resource builder.</param>
    /// <param name="agent">The AgentCore runtime resource builder returned by <see cref="AddAgentCoreRuntime{TProject}"/>.</param>
    /// <returns>The destination resource builder for further chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project already has a reference to another AgentCore agent.
    /// </exception>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<ProjectResource> agent)
        where TDestination : IResourceWithEnvironment
    {
        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .FirstOrDefault();

        if (annotation is null)
        {
            return builder.WithReference(agent as IResourceBuilder<IResourceWithServiceDiscovery>);
        }

        var hasExistingReference = builder.Resource.Annotations
            .OfType<AgentCoreReferenceAnnotation>()
            .Any();

        if (hasExistingReference)
        {
            throw new InvalidOperationException(
                $"Project '{builder.Resource.Name}' already has a WithReference to an AgentCore agent. " +
                "Only one AgentCore runtime reference is supported per project because the " +
                "AWS_ENDPOINT_URL_BEDROCK_AGENTCORE environment variable can only point to a single endpoint.");
        }

        builder.Resource.Annotations.Add(new AgentCoreReferenceAnnotation());

        builder.WithEnvironment(async context =>
        {
            if (context.ExecutionContext.IsPublishMode)
            {
                return;
            }

            await annotation.EmulatorStarted.Task;
            context.EnvironmentVariables["AWS_ENDPOINT_URL_BEDROCK_AGENTCORE"] =
                $"http://localhost:{annotation.RuntimeEmulatorPort}";
        });

        return builder;
    }

    /// <summary>
    /// Configures the chat app to use streaming (SSE) mode for this agent.
    /// </summary>
    /// <param name="agentApp">The agent resource builder.</param>
    /// <returns>The resource builder for further chaining.</returns>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<ProjectResource> WithStreaming(
        this IResourceBuilder<ProjectResource> agentApp)
    {
        var annotation = agentApp.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "WithStreaming can only be called on an AgentCore runtime resource. " +
                "Use AddAgentCoreRuntime<T>() to create it.");

        annotation.IsStreaming = true;

        return agentApp;
    }

    /// <summary>
    /// Adds an embedded memory emulator and wires it to the agent application.
    /// Sets the <c>AWS_AGENTCORE_MEMORY_ID</c> and <c>AWS_AGENTCORE_SERVICE_ENDPOINT</c>
    /// environment variables on the agent resource.
    /// </summary>
    /// <param name="agentApp">The agent resource builder.</param>
    /// <returns>The resource builder for further chaining.</returns>
    [Experimental(Constants.ASPIREAWSAGENTCORE001)]
    public static IResourceBuilder<ProjectResource> WithInMemory(
        this IResourceBuilder<ProjectResource> agentApp)
    {
        var annotation = agentApp.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "WithInMemory can only be called on an AgentCore runtime resource. " +
                "Use AddAgentCoreRuntime<T>() to create it.");

        annotation.HasMemory = true;

        agentApp.WithEnvironment("AWS_AGENTCORE_MEMORY_ID", "localdev-memory");

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
}
#endif
