// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Runtime.CompilerServices;
using AgentCoreTestApp.StreamingAgent.Models;
using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentCore(options =>
{
    options.ModelId = "global.anthropic.claude-opus-4-7";
    options.AgentOptions = new ChatClientAgentOptions
    {
        ChatOptions = new()
        {
            Tools = [AIFunctionFactory.Create(GetWeather)]
        }
    };
});

var app = builder.Build();

app.MapAgentCore<PromptRequest>(
    (PromptRequest request, ChatClientAgent agent, AgentCoreRuntimeContext context,
        ILogger<Program> logger, CancellationToken cancellationToken) =>
    {
        logger.LogInformation("Streaming invocation — SessionId={SessionId}", context.SessionId);

        return Stream(cancellationToken);

        async IAsyncEnumerable<string> Stream([EnumeratorCancellation] CancellationToken ct = default)
        {
            var session = await agent.CreateSessionAsync(cancellationToken: ct);

            await foreach (var update in agent.RunStreamingAsync(
                request.Prompt ?? "Hello!", session: session, cancellationToken: ct))
            {
                var text = update.Text;
                if (!string.IsNullOrEmpty(text))
                    yield return text;
            }
        }
    });

app.Run();

[Description("Gets the current weather for a given location.")]
static string GetWeather([Description("The city or location to get weather for.")] string location)
    => $"The current weather in {location} is 72°F and sunny.";
