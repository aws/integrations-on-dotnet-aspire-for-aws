// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

public partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the default CPU value, in CPU units, for an AWS Fargate Express task definition.
    /// </summary>
    /// <remarks>
    /// Default is 1024 CPU units (1 vCPU).
    /// </remarks>
    public virtual double? ECSFargateExpressCpu => 1024;

    /// <summary>
    /// Gets the default memory size, in mebibytes (MiB), allocated for an AWS Fargate Express task.
    /// </summary>
    /// <remarks>
    /// Default is 2048 MiB (2 GiB).
    /// </remarks>
    public virtual double? ECSFargateExpressMiB => 2048;

    /// <summary>
    /// Gets the default container port used for ECS Fargate Express deployments.
    /// </summary>
    /// <remarks>
    /// Default is port 8080.
    /// </remarks>
    public virtual double? ECSFargateExpressContainerPort => 8080;

    /// <summary>
    /// Applies default values to the specified properties for configuring an AWS CloudFormation Express Gateway
    /// service, if they are not already set.
    /// </summary>
    /// <param name="props">The properties object to which default values will be applied. Properties that are null or empty will be set to
    /// recommended defaults for an Express Gateway service.</param>
    /// <exception cref="InvalidDataException">Thrown if the <paramref name="props"/>.PrimaryContainer property is not set or is not of type <see
    /// cref="CfnExpressGatewayService.ExpressGatewayContainerProperty"/>.</exception>
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

        EnsureECSExpressVpcEndpoints();
    }

    private bool _ecsExpressVpcEndpointsCreated;

    /// <summary>
    /// Creates VPC Interface Endpoints for ECR and a Gateway Endpoint for S3 so that ECS Express
    /// tasks running in public subnets without a public IP can pull images via AWS PrivateLink.
    /// </summary>
    /// <remarks>
    /// ECS Express tasks land in public subnets but the API does not support assigning a public IP,
    /// so they have no outbound internet access. VPC endpoints provide a private path to ECR and S3
    /// without requiring a NAT Gateway. Safe to call multiple times; endpoints are created only once.
    /// </remarks>
    protected virtual void EnsureECSExpressVpcEndpoints()
    {
        if (_ecsExpressVpcEndpointsCreated) return;
        _ecsExpressVpcEndpointsCreated = true;

        var vpc = GetDefaultVpc();
        var stack = EnvironmentResource.CDKStack;
        var subnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC };

        new InterfaceVpcEndpoint(stack, "ECSExpressEcrApiEndpoint", new InterfaceVpcEndpointProps
        {
            Vpc = vpc,
            Service = InterfaceVpcEndpointAwsService.ECR,
            PrivateDnsEnabled = true,
            Subnets = subnets
        });

        new InterfaceVpcEndpoint(stack, "ECSExpressEcrDkrEndpoint", new InterfaceVpcEndpointProps
        {
            Vpc = vpc,
            Service = InterfaceVpcEndpointAwsService.ECR_DOCKER,
            PrivateDnsEnabled = true,
            Subnets = subnets
        });

        // ECR stores image layers in S3; gateway endpoints are free and route-table based
        new GatewayVpcEndpoint(stack, "ECSExpressS3Endpoint", new GatewayVpcEndpointProps
        {
            Vpc = vpc,
            Service = GatewayVpcEndpointAwsService.S3
        });
    }
}
