#if NET10_0_OR_GREATER
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.AgentCore;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.Dotnet;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

#pragma warning disable ASPIREAWSAGENTCORE001, ASPIREAWSPUBLISHERS001

/// <summary>
/// Covers the Aspire 17 <see cref="DotnetProjectResource"/> overloads that sit alongside the ProjectResource ones.
/// </summary>
public class DotnetProjectResourceTests
{
    // AddDotnetProject needs a real project file.
    private static string CreateProjectFile()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, "Agent.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        return path;
    }

    [Fact]
    public void AddAgentCoreRuntime_WithProjectPath_AddsDotnetProjectWithAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var agent = builder.AddAgentCoreRuntime("my-agent", CreateProjectFile())
            .WithAgentCoreStreaming()
            .WithAgentCoreMemory();

        var annotation = agent.Resource.Annotations.OfType<AgentCoreRuntimeAnnotation>().Single();
        Assert.True(annotation.IsStreaming);
        Assert.True(annotation.HasMemory);
    }

    [Fact]
    public void WithAgentCoreStreaming_ThrowsOnPlainDotnetProject()
    {
        var builder = DistributedApplication.CreateBuilder();

        var project = builder.AddDotnetProject("plain", CreateProjectFile());

        Assert.Throws<InvalidOperationException>(() => project.WithAgentCoreStreaming());
    }

    [Fact]
    public void PublishAs_AddsPublishAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();
        var project = builder.AddDotnetProject("svc", CreateProjectFile())
            .PublishAsECSFargateService()
            .PublishAsECSFargateServiceWithALB()
            .PublishAsECSFargateExpressService()
            .PublishAsAgentCoreRuntime();

        var annotations = project.Resource.Annotations;
        Assert.Single(annotations.OfType<PublishECSFargateServiceAnnotation>());
        Assert.Single(annotations.OfType<PublishECSFargateServiceWithALBAnnotation>());
        Assert.Single(annotations.OfType<PublishECSFargateServiceExpressAnnotation>());
        Assert.Single(annotations.OfType<PublishAgentCoreRuntimeAnnotation>());
    }

    // A custom image builder written before the IResource overload existed must fail clearly, not silently.
    [Fact]
    public async Task LegacyImageBuilder_ThrowsNotSupportedForDotnetProjectResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var project = builder.AddDotnetProject("svc", CreateProjectFile());

        ITarballContainerImageBuilder legacy = new LegacyImageBuilder();

        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.CreateTarballImageAsync((IResource)project.Resource));
    }

    private sealed class LegacyImageBuilder : ITarballContainerImageBuilder
    {
        public Task<string> CreateTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default) => Task.FromResult("x");
    }
}
#endif
