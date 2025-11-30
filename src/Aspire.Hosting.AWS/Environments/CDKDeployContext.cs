// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Utils.Internal;
using Aspire.Hosting.Pipelines;
using Humanizer;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Aspire.Hosting.AWS.Environments;

#pragma warning disable ASPIREPUBLISHERS001

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKDeployContext(IProcessCommandService processCommandService, ILogger<CDKPublishingContext> logger)
{
    public async Task ExecuteCDKDeployAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Initiating CDK deploy", cancellationToken);
        try
        {
            var cdkDeployCommand = "cdk deploy --require-approval never --app .";
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

            var exitCode = processCommandService.RunProcess(logger, shellCommand, arguments, environment.CDKApp.Outdir, streamOutputToLogger: true);
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
}
