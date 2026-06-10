// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using AWS.AgentCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentCoreTestApp.Agent;


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
                        AIFunctionFactory.Create(Agent.GetAppInfo)
                    ]
                }
            };
        });
    }
}