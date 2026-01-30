// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Deployment.Services;

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREPIPELINES003

/// <summary>
/// Used to create tarball for <see cref="ProjectResource"/>. When publishing for CDK container images must be
/// packaged up as tarballs. This allows the container be deployed to ECR as part of the CDK deploy.
/// </summary>
public interface ITarballContainerImageBuilder
{
    /// <summary>
    /// Creates a tarball from the local container build image for the Aspire project.
    /// </summary>
    /// <param name="resource">The Aspire <see cref="ProjectResource"/> that is built as a container image</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The path to where the tarball is created.</returns>
    Task<string> CreateTarballImageAsync(ProjectResource resource, CancellationToken cancellationToken = default(CancellationToken));
}