// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the memory size, in megabytes, allocated to the Lambda function.
    /// </summary>
    /// <remarks>
    /// Default is 512 MB.
    /// </remarks>
    public virtual double? LambdaFunctionMemorySize => 512;

    /// <summary>
    /// Gets the default timeout, in seconds, for Lambda function execution.
    /// </summary>
    /// <remarks>
    /// Default is 30 seconds.
    /// </remarks>
    public virtual int LambdaFunctionTimeout => 30;

    /// <summary>
    /// Applies default configuration values to the specified Lambda function properties if they are not already set.
    /// </summary>
    /// <param name="props">The Lambda function properties to which default values will be applied. Properties such as memory size, timeout,
    /// and runtime may be set if they are not already specified.</param>
    /// <param name="lambdaProjectResource">The Lambda project resource used to determine the target framework and other project metadata required for
    /// setting defaults.</param>
    /// <exception cref="InvalidOperationException">Thrown if the target .NET framework version cannot be determined from the project metadata, or if the target
    /// framework is not supported.</exception>
    protected internal virtual void ApplyLambdaFunctionDefaults(FunctionProps props, LambdaProjectResource lambdaProjectResource)
    {
        if (!props.MemorySize.HasValue)
            props.MemorySize = LambdaFunctionMemorySize;
        if (props.Timeout == null)
            props.Timeout = Duration.Seconds(LambdaFunctionTimeout);

        if (props.Runtime == null)
        {
            var targetFramework = ProjectUtilities.LookupTargetFrameworkFromProjectFile(lambdaProjectResource.GetProjectMetadata().ProjectPath);
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new InvalidOperationException($"Unable to determine target .NET version for Lambda function.");
            }

            switch (targetFramework)
            {
                case "net8.0":
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                case "net9.0":
                    // Fallback to .NET 8 for non-LTS assuming deployment package will be self contained.
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                case "net10.0":
                    props.Runtime = Runtime.DOTNET_10;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported target framework '{targetFramework}' for Lambda function.");
            }
        }
    }    
}
