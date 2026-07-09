var builder = DistributedApplication.CreateBuilder(args);

// Non-streaming agent with memory
var agent = builder.AddAgentCoreRuntime<Projects.AgentCoreTestApp_Agent>("AgentCoreTestApp-Agent")
    .WithAgentCoreMemory();

// Streaming agent
builder.AddAgentCoreRuntime<Projects.AgentCoreTestApp_StreamingAgent>("AgentCoreTestApp-StreamingAgent")
    .WithAgentCoreStreaming()
    .WithAgentCoreMemory();

// Chat UI that invokes the agent via the AWS SDK
// WithReference injects AWS_ENDPOINT_URL_BEDROCK_AGENTCORE so the SDK routes to the emulator
builder.AddProject<Projects.AgentCoreTestApp_ChatUI>("ChatUI")
    .WithReference(agent);

builder.Build().Run();
