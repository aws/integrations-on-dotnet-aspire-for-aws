// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.ComponentModel;
using AgentCore.Agent.Models;
using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;

namespace AgentCore.Agent;

public class Agent(ChatClientAgent chatAgent, ILogger<Agent> logger)
{
    [AgentCoreHandler]
    public async Task<string> HandleInvocation(
        PromptRequest request,
        AgentCoreRuntimeContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Invocation — SessionId={SessionId}, RequestId={RequestId}",
            context.SessionId, context.RequestId);

        var session = await chatAgent.CreateSessionAsync(cancellationToken: cancellationToken);

        var response = await chatAgent.RunAsync(
            request.Prompt ?? "Hello!", session: session, cancellationToken: cancellationToken);

        return response.ToString();
    }

    [AgentCorePing]
    public object Ping() => new { status = "Healthy", time_of_last_update = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

    [Description("Gets the current weather for a given location.")]
    public static string GetWeather([Description("The city or location to get weather for.")] string location)
        => $"The current weather in {location} is 72°F and sunny.";

    [Description("Gets the current date and time in UTC.")]
    public static string GetCurrentTime()
        => $"The current UTC time is {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
}
