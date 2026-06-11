// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSAGENTCORE001

var builder = DistributedApplication.CreateBuilder(args);

// Register a non-streaming agent with in-memory short-term memory.
// AddAgentCoreRuntime starts embedded runtime + chat emulators — no Docker needed.
var agent = builder.AddAgentCoreRuntime<Projects.AgentCore_Agent>(
    "AgentCore-Agent", new() { IncludeEmulatorLogs = true })
    .WithInMemory();

// Register a streaming agent.
// WithStreaming tells the chat app to use SSE streaming mode.
builder.AddAgentCoreRuntime<Projects.AgentCore_StreamingAgent>(
    "AgentCore-StreamingAgent", new() { IncludeEmulatorLogs = true })
    .WithStreaming()
    .WithInMemory();

// WithReference injects AWS_ENDPOINT_URL_BEDROCK_AGENTCORE into the ChatUI project
// so the AWS SDK routes requests to the local runtime emulator automatically.
builder.AddProject<Projects.AgentCore_ChatUI>("ChatUI")
    .WithHttpEndpoint(name: "http")
    .WithReference(agent);

builder.Build().Run();
