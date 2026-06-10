using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using AgentCoreTestApp.Agent.Models;
using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;

namespace AgentCoreTestApp.Agent;

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

    [Description("Returns runtime information about this application as a JSON string. Call this when asked about the app's name, architecture, framework, or whether it is running as NativeAOT. Return the JSON result directly to the user without modification.")]
    public static string GetAppInfo()
    {
        var isAot = typeof(object).Assembly.Location == string.Empty;
        return JsonSerializer.Serialize(new
        {
            appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown",
            isNativeAot = isAot,
            framework = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.OSArchitecture.ToString(),
            os = RuntimeInformation.OSDescription
        });
    }
}