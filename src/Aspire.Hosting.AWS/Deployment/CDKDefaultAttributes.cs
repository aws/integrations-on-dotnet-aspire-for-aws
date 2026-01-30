// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type IVpc.
/// This configures what should be used as the default VPC when creating any CDK constructs that require a <see cref="Amazon.CDK.AWS.EC2.IVpc"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultVpcAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.ECS.ICluster"/>.
/// This will be used when creating any CDK constructs like Fargate services that require an ECS Cluster.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSClusterAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.EC2.ISecurityGroup"/>.
/// This will be used as the default security group added to all Fargate services. When making references to other published resources
/// like ElastiCache clusters this security group will be used for making security group to security group ingress rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSClusterSecurityGroupAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.IAM.IRole"/>.
/// This will be used for the ECS Fargate Express service execution role.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSExpressExecutionRoleAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.IAM.IRole"/>.
/// This will be used for the ECS Fargate Express service infrastructure role.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultECSExpressInfrastructureRoleAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.ElastiCache.CfnSubnetGroup"/>.
/// This will be used as the default subnet group for ElastiCache provisioned clusters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheCfnSubnetGroupAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.EC2.ISecurityGroup"/>.
/// This will be used as the default security group added to all Fargate services. When making references to other published resources
/// this security group will be used for making security group to security group ingress rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheNodeSecurityGroupAttribute : Attribute
{

}

/// <summary>
/// Attribute applied on a property or field in the CDK deployment stack of type <see cref="Amazon.CDK.AWS.EC2.ISecurityGroup"/>.
/// This will be used as the default security group added to all Fargate services. When making references to other published resources
/// this security group will be used for making security group to security group ingress rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DefaultElastiCacheServerlessSecurityGroupAttribute : Attribute
{

}