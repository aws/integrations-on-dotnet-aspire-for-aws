// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.AWS.Lambda;

namespace Aspire.Hosting.AWS.Deployment.Services;

/// <summary>
/// Implementors will create a zip Lambda deployment bundle from the Lambda Aspire resource.
/// The default implementation uses the .NET CLI tool <see href="https://www.nuget.org/packages/Amazon.Lambda.Tools">Amazon.Lambda.Tools</see>
/// to perform the packaging.
/// </summary>
public interface ILambdaDeploymentPackager
{
    /// <summary>
    /// Creates the zip Lambda deployment bundle for the <see cref="LambdaProjectResource"/>. 
    /// </summary>
    /// <param name="lambdaFunction">The Aspire resource representing the Lambda function.</param>
    /// <param name="outputDirectory">The output directory where the zip deployment bundle will be created.</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns></returns>
    Task<LambdaDeploymentPackagerResult> CreateDeploymentPackageAsync(LambdaProjectResource lambdaFunction, string outputDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// The results of the <see cref="ILambdaDeploymentPackager.CreateDeploymentPackageAsync"/> creating the
/// zip deployment bundle from the Lambda Aspire resource.
/// </summary>
public class LambdaDeploymentPackagerResult
{
    /// <summary>
    /// True if the packaging process was successful.
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// The location of the Lambda deployment bundle.
    /// </summary>
    public string? LocalLocation { get; init; }
}