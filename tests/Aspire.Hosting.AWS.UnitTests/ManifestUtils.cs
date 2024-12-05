// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspire.Hosting.Utils;

// copied from https://github.com/dotnet/aspire/blob/13bcaa03819f5e342d96bdffed92a50c7175198d/tests/Aspire.Hosting.Tests/Utils/ManifestUtils.cs

public sealed class ManifestUtils
{
    public static async Task<JsonNode> GetManifest(IResource resource, string? manifestDirectory = null)
    {
        var node = await GetManifestOrNull(resource, manifestDirectory);
        Assert.NotNull(node);
        return node;
    }

    public static async Task<JsonNode?> GetManifestOrNull(IResource resource, string? manifestDirectory = null)
    {
        manifestDirectory ??= Environment.CurrentDirectory;

        using var ms = new MemoryStream();
        var writer = new Utf8JsonWriter(ms);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);
        writer.WriteStartObject();
        var context = new ManifestPublishingContext(executionContext, Path.Combine(manifestDirectory, "manifest.json"), writer);
        await WriteResourceAsync(context, resource);
        writer.WriteEndObject();
        writer.Flush();
        ms.Position = 0;
        var obj = JsonNode.Parse(ms);
        Assert.NotNull(obj);
        var resourceNode = obj[resource.Name];
        return resourceNode;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "WriteResourceAsync")]
    private static extern Task WriteResourceAsync(ManifestPublishingContext context, IResource resource);
}