var builder = DistributedApplication.CreateBuilder(args);

// Non-streaming agent with memory
var agent = builder.AddAgentCoreRuntime("AgentCoreTestApp-Agent", "../AgentCoreTestApp.Agent/AgentCoreTestApp.Agent.csproj")
    .WithAgentCoreMemory();

// Streaming agent
builder.AddAgentCoreRuntime("AgentCoreTestApp-StreamingAgent", "../AgentCoreTestApp.StreamingAgent/AgentCoreTestApp.StreamingAgent.csproj")
    .WithAgentCoreStreaming()
    .WithAgentCoreMemory();

// Chat UI that invokes the agent via the AWS SDK
// WithReference injects AWS_ENDPOINT_URL_BEDROCK_AGENTCORE so the SDK routes to the emulator
builder.AddDotnetProject("ChatUI", "../AgentCoreTestApp.ChatUI/AgentCoreTestApp.ChatUI.csproj")
    .WithReference(agent);

builder.Build().Run();
