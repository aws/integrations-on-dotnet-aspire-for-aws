// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.ECS;

namespace Aspire.Hosting.AWS.Environments.CDKDefaultsProviders;

public partial class CDKDefaultsProvider
{
    public virtual double? ECSFargateExpressCpu => 1024;

    public virtual double? ECSFargateExpressMiB => 2048;

    public virtual double? ECSFargateExpressContainerPort => 8080;

    protected internal virtual void ApplyCfnExpressGatewayServiceDefaults(CfnExpressGatewayServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = GetDefaultECSCluster().ClusterName;
        if (string.IsNullOrEmpty(props.Cpu))
            props.Cpu = ECSFargateExpressCpu.ToString();
        if (string.IsNullOrEmpty(props.Memory))
            props.Memory = ECSFargateExpressMiB.ToString();

        var primaryContainer = props.PrimaryContainer as CfnExpressGatewayService.ExpressGatewayContainerProperty;
        if (primaryContainer == null)
            throw new InvalidDataException("PrimaryContainer must be set and of type ExpressGatewayContainerProperty.");

        if (!primaryContainer.ContainerPort.HasValue)
            primaryContainer.ContainerPort = ECSFargateExpressContainerPort;

        if (string.IsNullOrEmpty(props.ExecutionRoleArn))
        {
            var role = GetDefaultECSExpressExecutionRole();
            props.ExecutionRoleArn = role.RoleArn;
        }

        if (string.IsNullOrEmpty(props.InfrastructureRoleArn))
        {
            var role = GetDefaultECSExpressInfrastructureRole();
            props.InfrastructureRoleArn = role.RoleArn;
        }

        if (props.NetworkConfiguration == null)
        {
            props.NetworkConfiguration = new CfnExpressGatewayService.ExpressGatewayServiceNetworkConfigurationProperty
            {
                SecurityGroups = new[]
                {
                    GetDefaultECSClusterSecurityGroup().SecurityGroupId
                },
                // Using public subnets because ECS Express chooses the ALB to be internet facing when using public subnets.
                // Otherwise if you use private subnets the ALB will be internal only to the VPC.
                Subnets = GetDefaultVpc().PublicSubnets.Select(s => s.SubnetId).ToArray()
            };
        }
    }    
}
