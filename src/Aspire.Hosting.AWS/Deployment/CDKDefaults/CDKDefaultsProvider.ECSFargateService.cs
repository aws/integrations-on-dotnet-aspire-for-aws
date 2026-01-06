// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    public virtual double? ECSFargateServiceCpu => 256;

    public virtual double? ECSFargateServiceMemoryLimitMiB => 512;

    public virtual double? ECSFargateServiceDesiredCount => 1;

    public virtual double? ECSFargateServiceMinHealthyPercent => 100;

    public virtual LogDriver? CreateECSFargateServiceLogDriver(string projectName)
    {
        return LogDrivers.AwsLogs(new AwsLogDriverProps
        {
            StreamPrefix = EnvironmentResource.CDKStack.StackName + "/" + projectName
        });
    }

    protected internal virtual void ApplyECSFargateServiceDefaults(FargateTaskDefinitionProps props)
    {
        if (props.Cpu == null)
            props.Cpu = ECSFargateServiceCpu;
        if (props.MemoryLimitMiB == null)
            props.MemoryLimitMiB = ECSFargateServiceMemoryLimitMiB;
    }

    protected internal virtual void ApplyECSFargateServiceDefaults(string projectName, ContainerDefinitionProps props)
    {
        if (props.Logging == null)
            props.Logging = CreateECSFargateServiceLogDriver(projectName);
    }

    protected internal virtual void ApplyECSFargateServiceDefaults(FargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = GetDefaultECSCluster();
        if (!props.DesiredCount.HasValue)
            props.DesiredCount = ECSFargateServiceDesiredCount;
        if (!props.MinHealthyPercent.HasValue)
            props.MinHealthyPercent = ECSFargateServiceMinHealthyPercent;
        if (props.SecurityGroups == null || props.SecurityGroups.Length == 0)
        {
            var defaultSecurityGroup = GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }

        // If there are no private subnets then the service is going in public subnets and so needs assigned IP addresses
        // to pull the image from ECR.
        if (GetDefaultVpc().PrivateSubnets == null || GetDefaultVpc().PrivateSubnets.Length == 0)
        {
            props.AssignPublicIp = true;
        }
    }    
}
