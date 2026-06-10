#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Net;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.AWS.Integ.Tests.AgentCore;

public class AgentCoreE2ETests
{
    [Fact]
    public async Task AgentCoreRuntime_StartsAndResponds()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AgentCoreTestApp_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService
            .WaitForResourceAsync("AgentCoreTestApp-Agent", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

        using var agentClient = app.CreateHttpClient("AgentCoreTestApp-Agent", "http");
        var pingResponse = await agentClient.GetAsync("/ping");
        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);
    }

    [Fact]
    public async Task StreamingAgent_StartsAndResponds()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AgentCoreTestApp_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService
            .WaitForResourceAsync("AgentCoreTestApp-StreamingAgent", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

        using var agentClient = app.CreateHttpClient("AgentCoreTestApp-StreamingAgent", "http");
        var pingResponse = await agentClient.GetAsync("/ping");
        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);
    }

    [Fact]
    public async Task ChatUI_ReceivesRuntimeEndpointVariable()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AgentCoreTestApp_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService
            .WaitForResourceAsync("ChatUI", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

        var chatUiResource = appHost.Resources.Single(r => r.Name == "ChatUI");
        var envAnnotations = chatUiResource.Annotations
            .OfType<EnvironmentCallbackAnnotation>();

        Assert.NotEmpty(envAnnotations);
    }
}
#endif
