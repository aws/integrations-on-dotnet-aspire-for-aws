// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.AWS.Lambda;

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