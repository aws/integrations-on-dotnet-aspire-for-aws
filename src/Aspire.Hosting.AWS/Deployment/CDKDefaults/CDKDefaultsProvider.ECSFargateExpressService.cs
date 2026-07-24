// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

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
    /// Gets the name given to the default container port mapping. ECS Fargate Express requires the primary
    /// ("Main") container to have a named TCP port mapping.
    /// </summary>
    /// <remarks>
    /// Default is "http".
    /// </remarks>
    public virtual string ECSFargateExpressContainerPortName => "http";

    /// <summary>
    /// Applies default values to the Fargate task definition used by an ECS Express Gateway service, if they
    /// are not already set. The Express service points at this task definition via its TaskDefinitionArn
    /// property, so container-hosting settings such as CPU, memory, and the execution role live here.
    /// </summary>
    /// <param name="props">The Fargate task definition properties to which default values will be applied.</param>
    protected internal virtual void ApplyCfnExpressGatewayServiceTaskDefinitionDefaults(FargateTaskDefinitionProps props)
    {
        if (props.Cpu == null)
            props.Cpu = ECSFargateExpressCpu;
        if (props.MemoryLimitMiB == null)
            props.MemoryLimitMiB = ECSFargateExpressMiB;
        if (props.ExecutionRole == null)
            props.ExecutionRole = GetDefaultECSExpressExecutionRole();
    }

    /// <summary>
    /// Applies default values to the container definition run by an ECS Express Gateway service's task
    /// definition, if they are not already set. This adds awslogs logging and the default container port.
    /// </summary>
    /// <param name="projectName">The name of the project, used to create the log stream prefix.</param>
    /// <param name="props">The container definition properties to which default values will be applied.</param>
    protected internal virtual void ApplyCfnExpressGatewayServiceContainerDefinitionDefaults(string projectName, ContainerDefinitionProps props)
    {
        if (props.Logging == null)
            props.Logging = CreateECSFargateServiceLogDriver(projectName);

        if (props.PortMappings == null || props.PortMappings.Length == 0)
        {
            // ECS Fargate Express requires the Main container to expose a named TCP port mapping.
            props.PortMappings = new[]
            {
                new PortMapping
                {
                    ContainerPort = ECSFargateExpressContainerPort ?? 8080,
                    Name = ECSFargateExpressContainerPortName,
                    Protocol = Protocol.TCP
                }
            };
        }
    }

    /// <summary>
    /// Applies default values to the specified properties for configuring an AWS CloudFormation Express Gateway
    /// service, if they are not already set. Container-hosting settings (CPU, memory, image, execution role,
    /// container port) live on the task definition; this method only sets the service-level defaults.
    /// </summary>
    /// <param name="props">The properties object to which default values will be applied. Properties that are null or empty will be set to
    /// recommended defaults for an Express Gateway service.</param>
    protected internal virtual void ApplyCfnExpressGatewayServiceDefaults(CfnExpressGatewayServiceProps props)
    {
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
