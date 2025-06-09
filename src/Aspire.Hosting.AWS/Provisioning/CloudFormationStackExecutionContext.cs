// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Provisioning;

internal sealed class CloudFormationStackExecutionContext(
    string stackName,
    string template)
{
    public string Template { get; } = template;

    public string StackName { get; } = stackName;

    public IDictionary<string, string> CloudFormationParameters { get; set; } = new Dictionary<string, string>();

    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

    public string? RoleArn { get; set; }

    public int StackPollingInterval { get; set; } = 3;

    public bool DisableDiffCheck { get; set; }

    public IList<string> DisabledCapabilities { get; set; } = [];
}
