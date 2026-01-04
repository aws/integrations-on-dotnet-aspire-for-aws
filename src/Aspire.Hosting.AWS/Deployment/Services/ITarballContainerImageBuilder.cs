// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Deployment.Services;

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREPIPELINES003

public interface ITarballContainerImageBuilder
{
    Task<string> BuildTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default(CancellationToken));
}