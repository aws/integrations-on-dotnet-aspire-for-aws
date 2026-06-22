// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Publishing.HoroscopeAgent;

[AgentCoreStartup]
public class Startup
{
    public void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.AddAgentCore(options =>
        {
            options.ModelId = "global.anthropic.claude-sonnet-4-6";
            options.AgentOptions = new ChatClientAgentOptions
            {
                ChatOptions = new()
                {
                    Tools =
                    [
                        AIFunctionFactory.Create(HoroscopeAgent.GetHoroscope),
                        AIFunctionFactory.Create(HoroscopeAgent.GetZodiacSign),
                        AIFunctionFactory.Create(HoroscopeAgent.GetCurrentDate)
                    ]
                }
            };
        });
    }
}
