// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// To avoid name collection with Amazon.CloudFormation.Model.InvalidOperationException
using InvalidOperationException = System.InvalidOperationException; 

namespace Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal record ResolvedParameter(string TemplateParameterName, string Value, bool IsSecret);

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKDeployStep(IProcessCommandService processCommandService, ILogger<CDKDeployStep> logger, IAWSEnvironmentService environmentService, IConfiguration configuration)
{
    private const string ParamPrefix = "Param-";

    public async Task ExecuteCDKDeployAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        // This is not meant for end-user usage. It is used by the test suite that executes the AppHost
        // without the Aspire CLI. In the test suite we often only want to run the publish step not the 
        // deploy but running the AppHost directly always run the full pipeline unless we specify this custom flag.
        if (environmentService.GetCommandLineArgs().Contains("--no-aws-deploy"))
            return;

        using var cfClient = environment.GetCloudFormationClient();

        await ExecuteCDKDeployCLIAsync(cfClient, context, environment, cancellationToken);
        await LogOutputParametersAsync(cfClient, context, environment, cancellationToken);
    }

    private async Task ExecuteCDKDeployCLIAsync(AmazonCloudFormationClient cfClient, PipelineStepContext context, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Initiating CDK deploy", cancellationToken);
        try
        {
            if (!SystemCapabilityEvaluator.IsCDKInstalled())
            {
                throw new InvalidOperationException("AWS CDK CLI is not installed. Please install the AWS CDK CLI to proceed. Visit https://docs.aws.amazon.com/cdk/v2/guide/getting-started.html for installation instructions.");
            }    

            var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";

            var resolvedParameters = ResolveParameters(environment);

            if (resolvedParameters.Count > 0)
            {
                logger.LogInformation("Resolved {ParameterCount} CloudFormation parameter(s)", resolvedParameters.Count);
                foreach (var param in resolvedParameters)
                {
                    var displayValue = param.IsSecret ? "****" : param.Value;
                    logger.LogInformation("Resolved parameter '{TemplateParameterName}' = '{Value}'", param.TemplateParameterName, displayValue);
                }
            }

            foreach (var param in resolvedParameters)
            {
                cdkDeployCommand += $" --parameters \"{param.TemplateParameterName}={param.Value}\"";
            }

            string shellCommand;
            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shellCommand = "powershell";
                arguments = $"-NoProfile -Command \"{cdkDeployCommand}\"";
            }
            else
            {
                shellCommand = "sh";
                arguments = $"-c \"{cdkDeployCommand}\"";
            }

            var environmentVariables = SdkUtilities.CreateDictionaryOfAWSCredentialsAndRegion(cfClient);

            var exitCode = processCommandService.RunProcess(logger, shellCommand, arguments, environment.CDKApp.Outdir, streamOutputToLogger: true, environmentVariables: environmentVariables);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"CDK deploy command failed with exit code {exitCode}");
            }

            await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deploy CDK application");
            await step.FailAsync($"Failed to deploy CDK application: {ex}", cancellationToken);
        }
    }

    internal IReadOnlyList<ResolvedParameter> ResolveParameters(AWSCDKEnvironmentResource environment)
    {
        var resolvedParameters = new List<ResolvedParameter>();

        foreach (var child in environment.CDKStack.Node.Children)
        {
            if (child is not Amazon.CDK.CfnParameter cfnParameter)
                continue;

            var constructId = cfnParameter.Node.Id;

            // Derive the configuration key
            string derivedKey;
            if (constructId.StartsWith(ParamPrefix, StringComparison.Ordinal))
            {
                derivedKey = constructId.Substring(ParamPrefix.Length);
            }
            else
            {
                derivedKey = constructId;
            }

            var configKey = $"Parameters:{derivedKey}";
            var value = configuration[configKey];            

            if (value != null)
            {
                var templateParameterName = ConvertToTemplateParameterName(constructId);
                resolvedParameters.Add(new ResolvedParameter(templateParameterName, value, cfnParameter.NoEcho));
            }
            else if (cfnParameter.Default == null)
            {
                throw new InvalidOperationException(
                    $"Required CloudFormation parameter '{constructId}' has no value configured. Set the value in configuration under the key '{configKey}'.");
            }
            // If value is null and the CfnParameter has a default value, skip the parameter
        }

        return resolvedParameters;
    }

    /// <summary>
    /// Converts a CDK construct ID to the CloudFormation template parameter name.
    /// CDK strips all non-alphanumeric characters when generating the logical ID
    /// for the CloudFormation template. For example, "Param-api-key" becomes "Paramapikey".
    /// </summary>
    internal static string ConvertToTemplateParameterName(string constructId)
    {
        return new string(constructId.Where(char.IsLetterOrDigit).ToArray());
    }

    private async Task LogOutputParametersAsync(AmazonCloudFormationClient cfClient, PipelineStepContext context, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Logging CloudFormation Stack output parameters", cancellationToken);
        try
        {
            
            var request = new DescribeStacksRequest { StackName = environment.CDKStack.StackName };
            var response = await cfClient.DescribeStacksAsync(request, cancellationToken).ConfigureAwait(false);

            // If the stack didn't exist then a StackNotFoundException would have been thrown.
            var stack = response.Stacks[0];

            if (stack.Outputs?.Any() == true)
            {
                logger.LogInformation("CloudFormation Stack Outputs:");
                foreach(var  output in stack.Outputs)
                {
                    logger.LogInformation("\t{OutputKey}: {OutputValue}", output.OutputKey, output.OutputValue);
                }
            }
            else
            {
                logger.LogInformation("No CloudFormation Stack output parameters");
            }

                await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log Stack output parameters");
            await step.FailAsync($"Failed to log Stack output parameters: {ex}", cancellationToken);
        }
    }
}
