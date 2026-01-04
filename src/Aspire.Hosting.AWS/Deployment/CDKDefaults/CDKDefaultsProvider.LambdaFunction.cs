// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.AWS.Utils;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    public virtual double? LambdaFunctionMemorySize => 512;

    public virtual int LambdaFunctionTimeout => 30;

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
