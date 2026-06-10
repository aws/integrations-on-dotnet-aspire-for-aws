#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.AgentCore;
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
    public void AddAgentCoreRuntime_UsesProjectTypeName_WhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>();

        Assert.Equal("FakeAgent", agent.Resource.Name);
    }

    [Fact]
    public void AddAgentCoreRuntime_UsesExplicitName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("custom-name");

        Assert.Equal("custom-name", agent.Resource.Name);
    }

    [Fact]
    public void AddAgentCoreRuntime_ReplacesUnderscoresWithDashes_InDefaultName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<Fake_Agent_Name>();

        Assert.Equal("Fake-Agent-Name", agent.Resource.Name);
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
    public void WithInMemory_SetsMemoryFlag()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent")
            .WithInMemory();

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.HasMemory);
    }

    [Fact]
    public void WithInMemory_ThrowsOnNonAgentCoreResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var project = builder.AddProject<FakeAgent>("plain-project", o => o.ExcludeLaunchProfile = true);

        Assert.Throws<InvalidOperationException>(() => project.WithInMemory());
    }

    [Fact]
    public void WithReference_FallsThroughToNativeServiceDiscovery_WhenNotAgentCore()
    {
        var builder = DistributedApplication.CreateBuilder();

        var plainProject = builder.AddProject<FakeAgent>("plain-project", o => o.ExcludeLaunchProfile = true);
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);

        var result = consumer.WithReference(plainProject);

        Assert.Same(consumer, result);
        var refAnnotation = consumer.Resource.Annotations
            .OfType<AgentCoreReferenceAnnotation>()
            .SingleOrDefault();
        Assert.Null(refAnnotation);
    }

    [Fact]
    public void WithReference_AddsReferenceAnnotationToConsumer()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);

        consumer.WithReference(agent);

        var refAnnotation = consumer.Resource.Annotations
            .OfType<AgentCoreReferenceAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(refAnnotation);
    }

    [Fact]
    public void WithReference_ThrowsOnDuplicateReference()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent");
        var consumer = builder.AddProject<FakeConsumer>("consumer", o => o.ExcludeLaunchProfile = true);

        consumer.WithReference(agent);

        Assert.Throws<InvalidOperationException>(() => consumer.WithReference(agent));
    }

    [Fact]
    public void WithReference_ThrowsOnSecondDifferentAgent()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent1 = builder.AddAgentCoreRuntime<FakeAgent>("agent-1");
        var agent2 = builder.AddAgentCoreRuntime<FakeConsumer>("agent-2");
        var consumer = builder.AddProject<Fake_Agent_Name>("consumer", o => o.ExcludeLaunchProfile = true);

        consumer.WithReference(agent1);

        Assert.Throws<InvalidOperationException>(() => consumer.WithReference(agent2));
    }

    [Fact]
    public void AddAgentCoreRuntime_ThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddAgentCoreRuntime<FakeAgent>("test"));
    }

    [Fact]
    public void WithStreaming_CanBeChainedWithWithInMemory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime<FakeAgent>("my-agent")
            .WithStreaming()
            .WithInMemory();

        var annotation = agent.Resource.Annotations
            .OfType<AgentCoreRuntimeAnnotation>()
            .Single();

        Assert.True(annotation.IsStreaming);
        Assert.True(annotation.HasMemory);
    }

    private sealed class Fake_Agent_Name : IProjectMetadata
    {
        public string ProjectPath => "fakeAgentName";
    }
}
#endif
