using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.AWS.Environments.DefaultProviderImplementations;
using Aspire.Hosting.AWS.Utils;
using System.Diagnostics.CodeAnalysis;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class DefaultProvider
{
    public static readonly DefaultProvider V1 = new V1DefaultProvider();

    protected DefaultProvider()
    {
    }

    public virtual string DeploymentTagName => "aspire:deployment-tag";

    /// <summary>
    /// Specifies the available compute services for the web application.
    /// </summary>
    public enum WebAppPublishTarget 
    {
        /// <summary>
        /// Deploy to AWS Elastic Container Service using the <a href="https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html">Express Mode</a>.
        /// Express mode deploys as an ECS service and a shared Application Load Balancer (ALB) across your Express mode services to route traffic to the service. 
        /// An HTTPS endpoint will be provisioned by default and a TargetGroup rule added to the ALB for the provisioned host name.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.CfnExpressGatewayService.html">CfnExpressGatewayService</a> construct is used to create the ECS Express Gateway service.
        /// </summary>
        /// <remarks>
        /// Port 8080 is assumed to be the container port the web application listens on.
        /// </remarks>
        ECSFargateExpressService,

        /// <summary>
        /// Deploy to AWS ECS Fargate Service with Application Load Balancer. This uses the CDK 
        /// <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs_patterns.ApplicationLoadBalancedFargateService.html">
        /// ApplicationLoadBalancedFargateService</a> construct. This construct will create an ECS Fargate service fronted by an 
        /// Application Load Balancer (ALB) to distribute incoming traffic across multiple instances of the web application.
        /// By default an HTTP endpoint will be provisioned.
        /// </summary>
        /// <remarks>
        /// Port 8080 is assumed to be the container port the web application listens on.
        /// </remarks>
        ECSFargateServiceWithALB
    }

    /// <summary>
    /// The default compute service to use when publishing web applications. The default value is <see cref="WebAppPublishTarget.ECSFargateExpressService"/>.
    /// </summary>
    public virtual WebAppPublishTarget DefaultWebAppPublishTarget { get; set; } = WebAppPublishTarget.ECSFargateExpressService;

    /// <summary>
    /// Specifies the available compute services for the console application.
    /// </summary>
    public enum ConsoleAppPublishTaret 
    {
        /// <summary>
        /// Deploy to as a service to the AWS Elastic Container Service (ECS). An ECS service is a continuously running set of tasks running the console application as a container.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_ecs.FargateService.html">FargateService</a> construct is used to create the ECS service.
        /// </summary>
        ECSFargateService
    }

    /// <summary>
    /// The default compute service to use when publishing console applications. For example background workers or message processors. The default value is <see cref="ConsoleAppPublishTaret.ECSFargateService"/>.
    /// </summary>
    public virtual ConsoleAppPublishTaret DefaultConsoleAppPublishTarget { get; set; } = ConsoleAppPublishTaret.ECSFargateService;

    /// <summary>
    /// Specifies the available compute services that support Lambda-based deployments.
    /// </summary>
    public enum LambdaComputeService 
    {
        /// <summary>
        /// Deploy project as to AWS Lambda as a function.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_lambda.Function.html">Function</a> construct is used to create the Lambda function.
        /// </summary>
        Lambda
    }

    /// <summary>
    /// The default compute service to use when publishing Lambda functions. The default value is <see cref="LambdaComputeService.Lambda"/>.
    /// </summary>
    public virtual LambdaComputeService DefaultLambdaProjectPublishTarget { get; set; } = LambdaComputeService.Lambda;

    public enum RedisPublishTarget
    {
        ElastiCacheNodeCluster,
        ElastiCacheServerlessCluster
    }

    public virtual RedisPublishTarget DefaultRedisPublishTarget { get; set; } = RedisPublishTarget.ElastiCacheNodeCluster;


    #region LambdaFunction
    public virtual double? LambdaFunctionMemorySize => 512;

    internal protected virtual void ApplyLambdaFunctionDefaults(string projectPath, FunctionProps props)
    {
        if (!props.MemorySize.HasValue)
            props.MemorySize = LambdaFunctionMemorySize;

        if (props.Runtime == null)
        {
            var targetFramework = ProjectUtilities.LookupTargetFrameworkFromProjectFile(projectPath);
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new InvalidOperationException($"Unable to determine target .NET version for Lambda function.");
            }

            switch (targetFramework)
            {
                case "net8.0":
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                case "net9.0":
                    // Fallback to .NET 8 for non-LTS assuming deployment package will be self contained.
                    props.Runtime = Runtime.DOTNET_8;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported target framework '{targetFramework}' for Lambda function.");
            }
        }
    }

    #endregion

    #region ECSFargateExpress
    public virtual double? ECSFargateExpressCpu => 1024;

    public virtual double? ECSFargateExpressMiB => 2048;

    public virtual double? ECSFargateExpressContainerPort => 8080;

    internal protected virtual void ApplyCfnExpressGatewayServiceDefaults(AWSCDKEnvironmentResource environment, CfnExpressGatewayServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = environment.DeploymentConstructProvider.GetDefaultECSCluster().ClusterName;
        if (string.IsNullOrEmpty(props.Cpu))
            props.Cpu = ECSFargateExpressCpu.ToString();
        if (string.IsNullOrEmpty(props.Memory))
            props.Memory = ECSFargateExpressMiB.ToString();

        var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
        if (primaryContainer == null)
            throw new InvalidDataException("PrimaryContainer must be set and of type ExpressGatewayContainerProperty.");

        if (!primaryContainer.ContainerPort.HasValue)
            primaryContainer.ContainerPort = ECSFargateExpressContainerPort;

        if (string.IsNullOrEmpty(props.ExecutionRoleArn))
        {
            var role = environment.DeploymentConstructProvider.GetDefaultECSExpressExecutionRole();
            props.ExecutionRoleArn = role.RoleArn;
        }

        if (string.IsNullOrEmpty(props.InfrastructureRoleArn))
        {
            var role = environment.DeploymentConstructProvider.GetDefaultECSExpressInfrastructureRole();
            props.InfrastructureRoleArn = role.RoleArn;
        }

        if (props.NetworkConfiguration == null)
        {
            props.NetworkConfiguration = new ExpressGatewayServiceNetworkConfigurationProperty
            {
                SecurityGroups = new[]
                {
                    environment.DeploymentConstructProvider.GetDefaultECSClusterSecurityGroup().SecurityGroupId
                },
                // Using public subnets because ECS Express chooses the ALB to be internet facing when using public subnets.
                // Otherwise if you use private subnets the ALB will be internal only to the VPC.
                Subnets = environment.DeploymentConstructProvider.GetDefaultVpc().PublicSubnets.Select(s => s.SubnetId).ToArray()
            };
        }
    }

    #endregion

    #region ECSFargateServiceWithALB
    public virtual double? ECSFargateServiceWithALBCpu => 1024;

    public virtual double? ECSFargateServiceWithALBMemoryLimitMiB => 2048;

    public virtual double? ECSFargateServiceWithALBDesiredCount => 2;

    public virtual double? ECSFargateServiceWithALBListenerPort => 80;

    public virtual double? ECSFargateServiceWithALBContainerPort => 8080;

    public virtual bool? ECSFargateServiceWithALBPublicLoadBalancer => true;

    public virtual double? ECSFargateServiceWithALBMinHealthyPercent => 100;

    internal protected virtual void ApplyECSFargateServiceWithALBDefaults(ApplicationLoadBalancedTaskImageOptions props)
    {
        if (!props.ContainerPort.HasValue)
            props.ContainerPort = ECSFargateServiceWithALBContainerPort;
    }

    internal protected virtual void ApplyECSFargateServiceWithALBDefaults(AWSCDKEnvironmentResource environment, ApplicationLoadBalancedFargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = environment.DeploymentConstructProvider.GetDefaultECSCluster();
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
            var defaultSecurityGroup = environment.DeploymentConstructProvider.GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }
    }

    #endregion

    #region ECSFargateService

    public virtual double? ECSFargateServiceCpu => 256;

    public virtual double? ECSFargateServiceMemoryLimitMiB => 512;

    public virtual double? ECSFargateServiceDesiredCount => 1;

    public virtual double? ECSFargateServiceMinHealthyPercent => 100;

    public virtual LogDriver? CreateECSFargateServiceLogDriver(AWSCDKEnvironmentResource environment, string projectName)
    {
        return LogDrivers.AwsLogs(new AwsLogDriverProps
        {
            StreamPrefix = environment.CDKStack.StackName + "/" + projectName
        });
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(FargateTaskDefinitionProps props)
    {
        if (props.Cpu == null)
            props.Cpu = ECSFargateServiceCpu;
        if (props.MemoryLimitMiB == null)
            props.MemoryLimitMiB = ECSFargateServiceMemoryLimitMiB;
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(AWSCDKEnvironmentResource environment, string projectName, ContainerDefinitionProps props)
    {
        if (props.Logging == null)
            props.Logging = CreateECSFargateServiceLogDriver(environment, projectName);
    }

    internal protected virtual void ApplyECSFargateServiceDefaults(AWSCDKEnvironmentResource environment, FargateServiceProps props)
    {
        if (props.Cluster == null)
            props.Cluster = environment.DeploymentConstructProvider.GetDefaultECSCluster();
        if (!props.DesiredCount.HasValue)
            props.DesiredCount = ECSFargateServiceDesiredCount;
        if (!props.MinHealthyPercent.HasValue)
            props.MinHealthyPercent = ECSFargateServiceMinHealthyPercent;
        if (props.SecurityGroups == null || props.SecurityGroups.Length == 0)
        {
            var defaultSecurityGroup = environment.DeploymentConstructProvider.GetDefaultECSClusterSecurityGroup();
            props.SecurityGroups = new[] { defaultSecurityGroup };
        }
    }

    #endregion

    #region ElasticCacheServerlessCluster

    public virtual string ElasticCacheServerlessClusterEngine => "valkey";

    public virtual string ElasticCacheServerlessMajorEngineVersion => "8";

    internal protected virtual void ApplyCfnServerlessCachePropsDefaults(AWSCDKEnvironmentResource environment, CfnServerlessCacheProps props)
    {
        if (props.Engine == null)
            props.Engine = ElasticCacheServerlessClusterEngine;
        if (props.MajorEngineVersion == null)
            props.MajorEngineVersion = ElasticCacheServerlessMajorEngineVersion;
    }

    #endregion

    #region ElasticCacheNodeCluster
    public virtual string ElasticCacheNodeClusterReplicationGroupDescription => "Cache for Aspire Application";

    public virtual string ElasticCacheNodeClusterEngine => "valkey";

    public virtual string? ElasticCacheNodeClusterEngineVersion => "8.2";

    public virtual string? ElasticCacheNodeClusterCacheNodeType => "cache.t3.micro";

    public virtual double? ElasticCacheNodeClusterNumCacheClusters => 2;

    public virtual bool? ElasticCacheNodeClusterAutomaticFailoverEnabled => true;

    public virtual double ElasticCacheNodeClusterPort => 6379;

    public virtual string ElasticCacheNodeClusterSubnetGroupDescription => "Subnet group for ElastiCache Node Cluster";

    public virtual string ElasticCacheNodeClusterParameterGroupFamily => "valkey8";

    public virtual string ElasticCacheNodeClusterParameterGroupDescription => "Parameter group for Node Cluster";

    public virtual bool? ElasticCacheNodeClusterTransitEncryptionEnabled => false;

    public virtual IDictionary<string, string>? ElasticCacheNodeClusterParameterGroupProperties => new Dictionary<string, string>
    {
    };

    internal protected virtual void ApplyCfnReplicationGroupPropsDefaults(AWSCDKEnvironmentResource environment, CfnReplicationGroupProps props)
    {
        if (props.ReplicationGroupDescription == null)
            props.ReplicationGroupDescription = ElasticCacheNodeClusterReplicationGroupDescription;
        if (props.CacheNodeType == null)
            props.CacheNodeType = ElasticCacheNodeClusterCacheNodeType;
        if (props.Engine == null)
            props.Engine = ElasticCacheNodeClusterEngine;
        if (props.EngineVersion == null)
            props.EngineVersion = ElasticCacheNodeClusterEngineVersion;
        if (props.NumCacheClusters == null)
            props.NumCacheClusters = ElasticCacheNodeClusterNumCacheClusters;
        if (props.AutomaticFailoverEnabled == null)
            props.AutomaticFailoverEnabled = ElasticCacheNodeClusterAutomaticFailoverEnabled;
        if (props.Port == null)
            props.Port = ElasticCacheNodeClusterPort;
        if (props.TransitEncryptionEnabled == null)
            props.TransitEncryptionEnabled = ElasticCacheNodeClusterTransitEncryptionEnabled;

        if (props.CacheSubnetGroupName == null)
            props.CacheSubnetGroupName = environment.DeploymentConstructProvider.GetDefaultElastiCacheCfnSubnetGroup().Ref;
        if (props.CacheParameterGroupName == null)
            props.CacheParameterGroupName = "default.valkey8.cluster.on";// environment.DeploymentConstructProvider.GetDefaultElastiCacheCfnParameterGroup().Ref;
        if (props.SecurityGroupIds == null)
            props.SecurityGroupIds = new[] { environment.DeploymentConstructProvider.GetDefaultElastiCacheSecurityGroup().SecurityGroupId };
    }

    #endregion

    #region Default CDK Constructions

    internal protected virtual IVpc CreateDefaultVpc(AWSCDKEnvironmentResource environment)
    {
        return new Vpc(environment.CDKStack, "DefaultVPC", new VpcProps
        {
            MaxAzs = 2
        });
    }

    internal protected virtual ICluster CreateDefaultECSCluster(AWSCDKEnvironmentResource environment)
    {
        return new Cluster(environment.CDKStack, "DefaultECSCluster", new ClusterProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc()
        });
    }

    internal protected virtual ISecurityGroup CreateDefaultECSClusterSecurityGroup(AWSCDKEnvironmentResource environment)
    {
        return new SecurityGroup(environment.CDKStack, "DefaultECSClusterSecurityGroup", new SecurityGroupProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc(),
            AllowAllOutbound = true
        });
    }

    internal protected virtual CfnSubnetGroup CreateDefaultElastiCacheCfnSubnetGroup(AWSCDKEnvironmentResource environment)
    {
        var subnetIds = environment.DeploymentConstructProvider.GetDefaultVpc().PrivateSubnets.Select(s => s.SubnetId).ToArray();
        return new CfnSubnetGroup(environment.CDKStack, "DefaultElastiCacheSubnetGroup", new CfnSubnetGroupProps
        {
            Description = environment.DefaultValuesProvider.ElasticCacheNodeClusterSubnetGroupDescription,
            SubnetIds = subnetIds
        });
    }

    internal protected virtual CfnParameterGroup CreateDefaultElastiCacheCfnParameterGroup(AWSCDKEnvironmentResource environment)
    {
        return new CfnParameterGroup(environment.CDKStack, "DefaultElastiCacheParameterGroup", new CfnParameterGroupProps
        {
            CacheParameterGroupFamily = environment.DefaultValuesProvider.ElasticCacheNodeClusterParameterGroupFamily,
            Description = environment.DefaultValuesProvider.ElasticCacheNodeClusterParameterGroupDescription,
            Properties = environment.DefaultValuesProvider.ElasticCacheNodeClusterParameterGroupProperties
        });
    }

    internal protected virtual ISecurityGroup CreateDefaultElastiCacheSecurityGroup(AWSCDKEnvironmentResource environment)
    {
        var defaultElastiCacheSecurityGroup = new SecurityGroup(environment.CDKStack, "DefaultElastiCacheSecurityGroup", new SecurityGroupProps
        {
            Vpc = environment.DeploymentConstructProvider.GetDefaultVpc(),
            AllowAllOutbound = true
        });

        defaultElastiCacheSecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(environment.DefaultValuesProvider.ElasticCacheNodeClusterPort), "Allow Redis access");
        return defaultElastiCacheSecurityGroup;
    }

    internal protected virtual IRole CreateDefaultECSExpressExecutionRole(AWSCDKEnvironmentResource environment)
    {
        return new Role(environment.CDKStack, "DefaultECSExpressExecutionRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryReadOnly"),
                ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"),
            }
        });
    }

    internal protected virtual IRole CreateDefaultECSExpressInfrastructureRole(AWSCDKEnvironmentResource environment)
    {
        return new Role(environment.CDKStack, "DefaultECSExpressInfrastructureRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonECSInfrastructureRoleforExpressGatewayServices"),
            }
        });
    }


    #endregion
}

