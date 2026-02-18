// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the number of CPU units allocated to the ECS Fargate service.
    /// </summary>
    /// <remarks>
    /// Default is 256 CPU units (0.25 vCPU).
    /// </remarks>
    public virtual double? ECSFargateServiceCpu => 256;

    /// <summary>
    /// Gets the memory limit, in mebibytes (MiB), allocated to the ECS Fargate service.
    /// </summary>
    /// <remarks>
    /// Default is 512 MiB (0.5 GiB).
    /// </remarks>
    public virtual double? ECSFargateServiceMemoryLimitMiB => 512;

    /// <summary>
    /// Gets the desired number of ECS Fargate service tasks to run.
    /// </summary>
    /// <remarks>
    /// Default is 1.
    /// </remarks>
    public virtual double? ECSFargateServiceDesiredCount => 1;

    /// <summary>
    /// Gets the minimum percentage of healthy tasks required for an ECS Fargate service deployment to be considered
    /// successful.
    /// </summary>
    /// <remarks>This value determines the lower limit on the number of running tasks that must remain healthy
    /// during a deployment. A value of 100 indicates that all tasks must be healthy for the deployment to
    /// proceed.</remarks>
    /// <remarks>
    /// Default is 100.
    /// </remarks>
    public virtual double? ECSFargateServiceMinHealthyPercent => 100;

    /// <summary>
    /// Creates the default log driver for ECS Fargate services.
    /// </summary>
    /// <param name="projectName">The project name is used to create the log stream prefix.</param>
    /// <returns>The CDK LogDriver construct</returns>
    public virtual LogDriver CreateECSFargateServiceLogDriver(string projectName)
    {
        return LogDrivers.AwsLogs(new AwsLogDriverProps
        {
            StreamPrefix = EnvironmentResource.CDKStack.StackName + "/" + projectName
        });
    }

    /// <summary>
    /// Applies default settings to the specified Fargate task definition properties if they are not
    /// already set.
    /// </summary>
    /// <param name="props">The Fargate task definition properties to which the setting values will be applied if not specified.</param>
    protected internal virtual void ApplyECSFargateServiceDefaults(FargateTaskDefinitionProps props)
    {
        if (props.Cpu == null)
            props.Cpu = ECSFargateServiceCpu;
        if (props.MemoryLimitMiB == null)
            props.MemoryLimitMiB = ECSFargateServiceMemoryLimitMiB;
    }

    /// <summary>
    /// Applies default configuration settings to the specified ECS Fargate service container definition if they are not
    /// already set.
    /// </summary>
    /// <param name="projectName">The name of the project to associate with the ECS Fargate service.</param>
    /// <param name="props">The container definition properties to which default settings will be applied.</param>
    protected internal virtual void ApplyECSFargateServiceDefaults(string projectName, ContainerDefinitionProps props)
    {
        if (props.Logging == null)
            props.Logging = CreateECSFargateServiceLogDriver(projectName);
    }

    /// <summary>
    /// Applies default values to the specified Fargate service properties if they are not already set.
    /// </summary>
    /// <remarks>
    /// If the VPC associated with the default ECS cluster does not have private subnets it is assumed that the service is public
    /// and AssignPublicIp is set to true.
    /// </remarks>
    /// <param name="props">The Fargate service properties to which default values will be applied. Properties that are null or unset will
    /// be assigned default values as appropriate.</param>
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
