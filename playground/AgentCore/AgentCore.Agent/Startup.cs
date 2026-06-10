// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentCore.Agent;

[AgentCoreStartup]
public class Startup
{
    public void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.AddAgentCore(options =>
        {
            options.ModelId = "global.anthropic.claude-opus-4-7";
            options.AgentOptions = new ChatClientAgentOptions
            {
                ChatOptions = new()
                {
                    Tools =
                    [
                        AIFunctionFactory.Create(Agent.GetWeather),
                        AIFunctionFactory.Create(Agent.GetCurrentTime)
                    ]
                }
            };
        });
    }
}
