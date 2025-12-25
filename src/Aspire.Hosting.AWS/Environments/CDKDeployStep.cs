// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime.Credentials;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Provisioning;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// To avoid name collection with Amazon.CloudFormation.Model.InvalidOperationException
using InvalidOperationException = System.InvalidOperationException; 

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKDeployStep(IProcessCommandService processCommandService, ILogger<CDKDeployStep> logger)
{
    public async Task ExecuteCDKDeployAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        using var cfClient = GetCloudFormationClient(environment);

        await ExecuteCDKDeployCLIAsync(cfClient, context, model, environment, cancellationToken);
        await LogOutputParametersAsync(cfClient, context, environment, cancellationToken);
    }

    private async Task ExecuteCDKDeployCLIAsync(AmazonCloudFormationClient cfClient, PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Initiating CDK deploy", cancellationToken);
        try
        {
            var cdkDeployCommand = "cdk deploy --no-notices --require-approval never --app .";
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

    private static AmazonCloudFormationClient GetCloudFormationClient(AWSCDKEnvironmentResource environment)
    {
        try
        {
            AmazonCloudFormationClient client;
            if (environment.AWSSDKConfig != null)
            {
                var config = environment.AWSSDKConfig.CreateServiceConfig<AmazonCloudFormationConfig>();

                var awsCredentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(config);
                client = new AmazonCloudFormationClient(awsCredentials, config);
            }
            else
            {
                client = new AmazonCloudFormationClient();
            }

            client.BeforeRequestEvent += SdkUtilities.ConfigureUserAgentString;

            return client;
        }
        catch (Exception e)
        {
            throw new AWSProvisioningException("Failed to construct AWS CloudFormation service client to provision AWS resources.", e);
        }
    }
}
