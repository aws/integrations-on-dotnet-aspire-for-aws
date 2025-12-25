// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS.Patterns;

namespace Aspire.Hosting.AWS.Environments.CDKDefaultsProviders;

public partial class CDKDefaultsProvider
{
    public virtual double? ECSFargateServiceWithALBCpu => 1024;

    public virtual double? ECSFargateServiceWithALBMemoryLimitMiB => 2048;

    public virtual double? ECSFargateServiceWithALBDesiredCount => 2;

    public virtual double? ECSFargateServiceWithALBListenerPort => 80;

    public virtual double? ECSFargateServiceWithALBContainerPort => 8080;

    public virtual bool? ECSFargateServiceWithALBPublicLoadBalancer => true;

    public virtual double? ECSFargateServiceWithALBMinHealthyPercent => 100;

    protected internal virtual void ApplyECSFargateServiceWithALBDefaults(ApplicationLoadBalancedTaskImageOptions props)
    {
        if (!props.ContainerPort.HasValue)
            props.ContainerPort = ECSFargateServiceWithALBContainerPort;
    }

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
