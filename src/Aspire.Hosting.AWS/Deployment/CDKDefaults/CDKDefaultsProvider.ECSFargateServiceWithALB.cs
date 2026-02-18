// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS.Patterns;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the default CPU value, in CPU units, for an ECS Fargate service with an Application Load Balancer.
    /// </summary>
    /// <remarks>
    /// Default is 1024 CPU units (1 vCPU).
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBCpu => 1024;

    /// <summary>
    /// Gets the default memory limit, in MiB, for an Amazon ECS Fargate service with an Application Load Balancer.
    /// </summary>
    /// <remarks>
    /// Default is 2048 MiB (2 GiB).
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBMemoryLimitMiB => 2048;

    /// <summary>
    /// Gets the desired number of ECS Fargate service tasks behind the Application Load Balancer.
    /// </summary>
    /// <remarks>
    /// Default is 2.
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBDesiredCount => 2;

    /// <summary>
    /// Gets the port number used by the Application Load Balancer listener for the ECS Fargate service.
    /// </summary>
    /// <remarks>
    /// Default is port 80.
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBListenerPort => 80;

    /// <summary>
    /// Gets the container port used by the ECS Fargate service with an Application Load Balancer (ALB).
    /// </summary>
    /// <remarks>
    /// Default is port 8080.
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBContainerPort => 8080;

    /// <summary>
    /// Gets a value indicating whether the Application Load Balancer for the ECS Fargate service is public.
    /// </summary>
    /// <remarks>
    /// Default is true.
    /// </remarks>
    public virtual bool? ECSFargateServiceWithALBPublicLoadBalancer => true;

    /// <summary>
    /// Gets the minimum percentage of healthy tasks required for the ECS Fargate service behind an Application Load
    /// Balancer.
    /// </summary>
    /// <remarks>
    /// Default is 100.
    /// </remarks>
    public virtual double? ECSFargateServiceWithALBMinHealthyPercent => 100;

    /// <summary>
    /// Applies default settings to the specified Fargate task image options if they are not
    /// </summary>
    /// <param name="props">The task image options to which default settings will be applied.</param>
    protected internal virtual void ApplyECSFargateServiceWithALBDefaults(ApplicationLoadBalancedTaskImageOptions props)
    {
        if (!props.ContainerPort.HasValue)
            props.ContainerPort = ECSFargateServiceWithALBContainerPort;
    }

    /// <summary>
    /// Applies default settings to the specified Application Load Balanced Fargate service properties if they are not.
    /// </summary>
    /// <param name="props">The props for the ApplicationLoadBalancedFargateService to which default settings will be applied.</param>
    protected internal virtual void ApplyECSFargateServiceWithALBDefaults(ApplicationLoadBalancedFargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = GetDefaultECSCluster();
        if (!props.Cpu.HasValue)
            props.Cpu = ECSFargateServiceWithALBCpu;
        if (!props.MemoryLimitMiB.HasValue)
            props.MemoryLimitMiB = ECSFargateServiceWithALBMemoryLimitMiB;
        if (!props.DesiredCount.HasValue)
            props.DesiredCount = ECSFargateServiceWithALBDesiredCount;
        if (!props.ListenerPort.HasValue)
            props.ListenerPort = ECSFargateServiceWithALBListenerPort;
        if (!props.PublicLoadBalancer.HasValue)
            props.PublicLoadBalancer = ECSFargateServiceWithALBPublicLoadBalancer;
        if (!props.MinHealthyPercent.HasValue)
            props.MinHealthyPercent = ECSFargateServiceWithALBMinHealthyPercent;
        if (props.SecurityGroups == null || props.SecurityGroups.Length == 0)
        {
            var defaultSecurityGroup = GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }
    }    
}
