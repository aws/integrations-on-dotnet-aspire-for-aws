// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Aspire.Hosting.AWS.Environments.Services;

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREPIPELINES003

public interface ITarballContainerImageBuilder
{
    Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default(CancellationToken));
}