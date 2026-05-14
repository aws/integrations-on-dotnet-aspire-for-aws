// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Deployment.Services;

/// <summary>
/// Builds a JavaScript application's static output by invoking its build script.
/// </summary>
public interface IStaticSiteBuilder
{
    /// <summary>
    /// Runs the build script for the given resource, injecting
    /// <paramref name="environmentVariables"/> into the build process.
    /// </summary>
    /// <param name="resource">The resource to build.</param>
    /// <param name="workingDirectory">The working directory for the build.</param>
    /// <param name="environmentVariables">Environment variables to expose during the build (e.g. VITE_* vars from Aspire references).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task BuildAsync(IResource resource, string workingDirectory, IDictionary<string, string> environmentVariables, CancellationToken cancellationToken);
}
