// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class CDKDestroyStep(ILogger<CDKDestroyStep> logger)
{
    // CloudFormation statuses for when the stack is in transition all end with IN_PROGRESS
    const string IN_PROGRESS_SUFFIX = "IN_PROGRESS";
    static readonly TimeSpan StackPollingDelay = TimeSpan.FromSeconds(3);
    
    public async Task ExecuteCDKDestroyAsync(PipelineStepContext context, DistributedApplicationModel model, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Executing aws destroy");
        
        environment.InitializeCDKApp(logger, Path.GetTempPath());

        using var cfClient = environment.GetCloudFormationClient();

        await ExecuteCDKDestroyCLIAsync(cfClient, context, environment, cancellationToken);
    }
    
    private async Task ExecuteCDKDestroyCLIAsync(AmazonCloudFormationClient cfClient, PipelineStepContext context, AWSCDKEnvironmentResource environment, CancellationToken cancellationToken)
    {
        var step = await context.ReportingStep.CreateTaskAsync($"Initiating CloudFormation Stack deletion: {environment.CDKStack.StackName}", cancellationToken);
        try
        {
            var deleteRequest = new DeleteStackRequest
            {
                StackName = environment.CDKStack.StackName
            };

            var mintimeStampForEvents = DateTime.UtcNow;
            await cfClient.DeleteStackAsync(deleteRequest, cancellationToken);

            await WaitStackToCompleteAsync(cfClient, environment.CDKStack.StackName, mintimeStampForEvents, cancellationToken);

            await step.SucceedAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete CloudFormation Stack");
            await step.FailAsync($"Failed to delete CloudFormation Stack: {ex}", cancellationToken);
        }
    }   
    
    private async Task<Stack?> WaitStackToCompleteAsync(AmazonCloudFormationClient cfClient, string stackName, DateTime mintimeStampForEvents, CancellationToken cancellationToken)
    {
        const int TIMESTAMP_WIDTH = 20;
        const int LOGICAL_RESOURCE_WIDTH = 40;
        const int RESOURCE_STATUS = 40;
        string mostRecentEventId = "";
        
        // Write header for the status table.
        logger.LogInformation("   ");
        logger.LogInformation(
            "Timestamp".PadRight(TIMESTAMP_WIDTH) + " " +
            "Logical Resource Id".PadRight(LOGICAL_RESOURCE_WIDTH) + " " +
            "Status".PadRight(RESOURCE_STATUS) + " ");
        logger.LogInformation(
            new string('-', TIMESTAMP_WIDTH) + " " +
            new string('-', LOGICAL_RESOURCE_WIDTH) + " " +
            new string('-', RESOURCE_STATUS) + " ");

        Stack? stack;
        do
        {
            await Task.Delay(StackPollingDelay, cancellationToken);
            stack = await GetExistingStackAsync(cfClient, stackName, cancellationToken);
            if (stack == null)
                break;
            
            var events = await GetLatestEventsAsync(cfClient, stackName, mintimeStampForEvents, mostRecentEventId, cancellationToken);
            if (events.Count > 0)
                mostRecentEventId = events[0].EventId;

            for (int i = events.Count - 1; i >= 0; i--)
            {
                string line =
                    events[i].Timestamp.GetValueOrDefault().ToLocalTime().ToString("g").PadRight(TIMESTAMP_WIDTH) + " " +
                    events[i].LogicalResourceId.PadRight(LOGICAL_RESOURCE_WIDTH) + " " +
                    events[i].ResourceStatus.ToString().PadRight(RESOURCE_STATUS);

                // To save screen space only show error messages.
                if (!events[i].ResourceStatus.ToString().EndsWith(IN_PROGRESS_SUFFIX) && !string.IsNullOrEmpty(events[i].ResourceStatusReason))
                    line += " " + events[i].ResourceStatusReason;

                logger.LogInformation(line);
            }

        } while (stack != null && stack.StackStatus.ToString().EndsWith(IN_PROGRESS_SUFFIX));

        return stack;
    }

    private async Task<List<StackEvent>> GetLatestEventsAsync(AmazonCloudFormationClient cfClient, string stackName, DateTime mintimeStampForEvents, string mostRecentEventId, CancellationToken cancellationToken)
    {
        bool noNewEvents = false;
        List<StackEvent> events = new List<StackEvent>();
        DescribeStackEventsResponse? response = null;
        do
        {
            var request = new DescribeStackEventsRequest() { StackName = stackName };
            if (response != null)
                request.NextToken = response.NextToken;

            try
            {
                response = await cfClient.DescribeStackEventsAsync(request, cancellationToken);
            }
            catch (AmazonCloudFormationException e) when 
                (e.StatusCode == System.Net.HttpStatusCode.BadRequest && string.Equals(e.ErrorCode, "ValidationError", StringComparison.InvariantCultureIgnoreCase))
            {
                // This will happen once the stack is completely deleted.
                break;
            }
            catch (Exception e)
            {
                throw new ApplicationException($"Error getting events for stack: {e.Message}", e);
            }
            foreach (var evnt in response.StackEvents ?? new List<StackEvent>())
            {
                if (string.Equals(evnt.EventId, mostRecentEventId) || evnt.Timestamp < mintimeStampForEvents)
                {
                    noNewEvents = true;
                    break;
                }

                events.Add(evnt);
            }

        } while (!noNewEvents && !string.IsNullOrEmpty(response.NextToken));

        return events;
    }
    
    public async Task<Stack?> GetExistingStackAsync(AmazonCloudFormationClient cfClient, string stackName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await cfClient.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName }, cancellationToken);
            if (response.Stacks == null || response.Stacks.Count != 1)
                return null;

            return response.Stacks[0];
        }
        catch (AmazonCloudFormationException)
        {
            return null;
        }
    }        
}