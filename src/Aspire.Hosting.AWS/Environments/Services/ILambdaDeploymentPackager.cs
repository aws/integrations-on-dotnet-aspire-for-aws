// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils.Internal;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Environments.Services;

public interface ILambdaDeploymentPackager
{
    Task<LambdaDeploymentPackagerOutput> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken);
}

public class LambdaDeploymentPackagerOutput
{
    public required bool Success { get; init; }
    public string? LocalLocation { get; init; }
}