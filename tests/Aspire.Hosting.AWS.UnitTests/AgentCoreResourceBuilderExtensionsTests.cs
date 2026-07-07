#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.AgentCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class AgentCoreResourceBuilderExtensionsTests
{
    private sealed class FakeAgent : IProjectMetadata
    {
        public string ProjectPath => "fakeAgent";
    }

    private sealed class FakeConsumer : IProjectMetadata
    {
        public string ProjectPath => "fakeConsumer";
    }

    [Fact]
    public void AddAgentCoreRuntime_AddsRuntimeAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
        Assert.False(annotation.IsStreaming);
        Assert.False(annotation.HasMemory);
        Assert.False(annotation.IncludeEmulatorLogs);
    }

    [Fact]
    public void AddAgentCoreRuntime_UsesExplicitName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("custom-name");

        Assert.Equal("custom-name", agent.Resource.Name);
    }

    [Fact]
    public void AddAgentCoreRuntime_SetsAspireManagedEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");

        var envCallbacks = agent.Resource.Annotations
            .OfType<EnvironmentCallbackAnnotation>();

        Assert.NotEmpty(envCallbacks);
    }

    [Fact]
    public void AddAgentCoreRuntime_WithOptions_SetsIncludeEmulatorLogs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent", new AgentCoreLocalOptions
        {
            IncludeEmulatorLogs = true
        });

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.IncludeEmulatorLogs);
    }

    [Fact]
    public void WithStreaming_SetsStreamingFlag()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent")
            .WithStreaming();

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.IsStreaming);
    }

    [Fact]
    public void WithStreaming_ThrowsOnNonAgentCoreResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var project = builder.AddProject<FakeAgent>("plain-project", o => o.ExcludeLaunchProfile = true);

        Assert.Throws<InvalidOperationException>(() => project.WithStreaming());
    }

    [Fact]
    public void WithAgentCoreMemory_SetsMemoryFlag()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent")
            .WithAgentCoreMemory();

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.HasMemory);
    }

    [Fact]
    public void WithAgentCoreMemory_ThrowsOnNonAgentCoreResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var project = builder.AddProject<FakeAgent>("plain-project", o => o.ExcludeLaunchProfile = true);

        Assert.Throws<InvalidOperationException>(() => project.WithAgentCoreMemory());
    }

    [Fact]
    public void WithReference_OnAgent_RecordsRelationshipForLifecycleHook()
    {
        // Consumers wire to AgentCore runtimes using Aspire's stock WithReference overload.
        // The AgentCore integration injects AWS_ENDPOINT_URL_BEDROCK_AGENTCORE during
        // BeforeStartEvent by walking the ResourceRelationshipAnnotation entries Aspire writes.
        // This test verifies the integration point — that the relationship the hook reads is present.
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);

        consumer.WithReference(agent);

        var pointsToAgent = consumer.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Any(r => r.Type == "Reference" && ReferenceEquals(r.Resource, agent.Resource));

        Assert.True(pointsToAgent);
    }

    [Fact]
    public async Task BeforeStartEvent_InjectsBedrockAgentCoreEndpoint_OnReferencingConsumer()
    {
        // End-to-end coverage of the lifecycle hook in EnsureReferenceHook:
        //  1. consumer.WithReference(agent) writes Aspire's ResourceRelationshipAnnotation
        //  2. our BeforeStartEvent handler walks those, finds the AgentCoreRuntimeAnnotation
        //  3. it adds an EnvironmentCallbackAnnotation that resolves AWS_ENDPOINT_URL_BEDROCK_AGENTCORE
        //
        // This is the contract test that catches Aspire-side breakages: if Aspire renames
        // the relationship type, stops writing it, restructures BeforeStartEvent, or changes
        // any of the annotation shapes our hook depends on, this test fails — even though the
        // surface API of AddAgentCoreRuntime/WithReference still compiles.
        var builder = CreatePublishModeBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);
        consumer.WithReference(agent);

        // Pre-resolve the agent's port — in production this happens in the agent's own
        // BeforeResourceStartedEvent handler, but that path requires a real orchestrator.
        // Completing EmulatorStarted unblocks our consumer-side env callback.
        var ann = agent.Resource.Annotations.OfType<AgentCoreRuntimeAnnotation>().Single();
        ann.RuntimeEmulatorPort = 12345;
        ann.EmulatorStarted.SetResult();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        await builder.Eventing.PublishAsync(new BeforeStartEvent(app.Services, model));

        // Use Run-mode execution context so the env callback writes the var (it skips publish mode by design).
        var envVars = await EvaluateEnvironmentVariables(consumer.Resource, app.Services);
        Assert.Equal("http://localhost:12345", envVars["AWS_ENDPOINT_URL_BEDROCK_AGENTCORE"]);

        // The hook also sets the runtime ARN under the standard reference convention so app code
        // reads the same IConfiguration key locally and when deployed. Locally it's the placeholder
        // "local-agent" (the emulator ignores it); deployment overrides it with the real ARN.
        Assert.Equal("local-agent", envVars["AWS__Resources__my-agent__AgentRuntimeArn"]);
    }

    [Fact]
    public async Task BeforeStartEvent_DoesNotInjectEndpoint_WhenConsumerHasNoAgentCoreReference()
    {
        // Guard against the hook injecting AWS_ENDPOINT_URL_BEDROCK_AGENTCORE on resources
        // that reference unrelated projects. The hook must only fire when the referenced
        // resource carries an AgentCoreRuntimeAnnotation.
        var builder = CreatePublishModeBuilder();

        // Need at least one AddAgentCoreRuntime call for the hook to be registered.
        builder.AddAgentCoreRuntime<FakeAgent>("unrelated-agent");

        var plainProject = builder.AddProject<FakeAgent>("plain-project", o => o.ExcludeLaunchProfile = true);
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);
        consumer.WithReference(plainProject);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        await builder.Eventing.PublishAsync(new BeforeStartEvent(app.Services, model));

        var envVars = await EvaluateEnvironmentVariables(consumer.Resource, app.Services);
        Assert.False(envVars.ContainsKey("AWS_ENDPOINT_URL_BEDROCK_AGENTCORE"));
    }

    [Fact]
    public async Task BeforeStartEvent_Throws_WhenConsumerReferencesMultipleAgentCoreRuntimes()
    {
        // AWS_ENDPOINT_URL_BEDROCK_AGENTCORE is a single-valued AWS SDK endpoint override;
        // pointing it at two distinct runtimes is unsupported. Validation moved from chain-time
        // to start-time when we removed the custom WithReference overload.
        var builder = CreatePublishModeBuilder();

        var agent1 = builder.AddAgentCoreRuntime<FakeAgent>("agent-1");
        var agent2 = builder.AddAgentCoreRuntime<FakeConsumer>("agent-2");
        var consumer = builder.AddProject<FakeAgent>("consumer", o => o.ExcludeLaunchProfile = true);
        consumer.WithReference(agent1);
        consumer.WithReference(agent2);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.Eventing.PublishAsync(new BeforeStartEvent(app.Services, model)));
    }

    // Build in Publish mode to skip Aspire's DCP wiring (DCP isn't available in unit tests).
    // Our EnsureReferenceHook subscribes to BeforeStartEvent regardless of mode, so the test
    // still exercises the hook end-to-end. The hook's runtime callback skips publish mode, so
    // EvaluateEnvironmentVariables synthesizes a Run-mode context for the env eval step.
    private static IDistributedApplicationBuilder CreatePublishModeBuilder()
        => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--publisher", "manifest"]
        });

    private static async Task<Dictionary<string, string>> EvaluateEnvironmentVariables(IResource resource, IServiceProvider services)
    {
        var result = new Dictionary<string, string>();
        var runContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run) { ServiceProvider = services });

        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            var raw = new Dictionary<string, object>();
            var ctx = new EnvironmentCallbackContext(runContext, resource, raw);
            await callback.Callback(ctx);
            foreach (var (k, v) in raw)
            {
                result[k] = v?.ToString() ?? string.Empty;
            }
        }

        return result;
    }

    [Fact]
    public void AddAgentCoreRuntime_ThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddAgentCoreRuntime<FakeAgent>("test"));
    }

    [Fact]
    public void WithStreaming_CanBeChainedWithWithAgentCoreMemory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent")
            .WithStreaming()
            .WithAgentCoreMemory();

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.IsStreaming);
        Assert.True(annotation.HasMemory);
    }

}
#endif
