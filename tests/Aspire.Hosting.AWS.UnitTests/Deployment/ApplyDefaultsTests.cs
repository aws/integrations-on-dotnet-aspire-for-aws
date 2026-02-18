// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Lambda;
using Xunit;
using static Amazon.CDK.AWS.ECS.CfnExpressGatewayService;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class ApplyDefaultsTests
{
    [Fact]
    public void ApplyCfnExpressGatewayServiceDefaults_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new CfnExpressGatewayServiceProps
        {
            PrimaryContainer = new ExpressGatewayContainerProperty()
        };

        // Act
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(props);

        // Assert
        Assert.NotNull(props.Cluster);
        Assert.Equal("1024", props.Cpu);
        Assert.Equal("2048", props.Memory);
        Assert.NotNull(props.ExecutionRoleArn);
        Assert.NotNull(props.InfrastructureRoleArn);

        var primaryContainer = props.PrimaryContainer as ExpressGatewayContainerProperty;
        Assert.NotNull(primaryContainer);
        Assert.Equal(8080, primaryContainer.ContainerPort);

        var expectedClusterName = environment.DefaultsProvider.GetDefaultECSCluster().ClusterName;
        Assert.Equal(expectedClusterName, props.Cluster);

        var expectedExecutionRoleArn = environment.DefaultsProvider.GetDefaultECSExpressExecutionRole().RoleArn;
        Assert.Equal(expectedExecutionRoleArn, props.ExecutionRoleArn);

        var expectedInfrastructureRoleArn = environment.DefaultsProvider.GetDefaultECSExpressInfrastructureRole().RoleArn;
        Assert.Equal(expectedInfrastructureRoleArn, props.InfrastructureRoleArn);
    }

    [Fact]
    public void ApplyCfnExpressGatewayServiceDefaults_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingCluster = "my-existing-cluster";
        var existingCpu = "512";
        var existingMemory = "1024";
        var existingPort = 3000;
        var existingExecutionRoleArn = "arn:aws:iam::123456789012:role/my-execution-role";
        var existingInfrastructureRoleArn = "arn:aws:iam::123456789012:role/my-infrastructure-role";

        var primaryContainer = new ExpressGatewayContainerProperty
        {
            ContainerPort = existingPort
        };

        var props = new CfnExpressGatewayServiceProps
        {
            Memory = existingMemory,
            Cpu = existingCpu,
            Cluster = existingCluster,
            PrimaryContainer = primaryContainer,
            ExecutionRoleArn = existingExecutionRoleArn,
            InfrastructureRoleArn = existingInfrastructureRoleArn,
        };

        // Act
        environment.DefaultsProvider.ApplyCfnExpressGatewayServiceDefaults(props);

        // Assert
        Assert.Equal(existingCluster, props.Cluster);
        Assert.Equal(existingCpu, props.Cpu);
        Assert.Equal(existingMemory, props.Memory);
        Assert.Equal(existingPort, primaryContainer!.ContainerPort);
        Assert.Equal(existingExecutionRoleArn, props.ExecutionRoleArn);
        Assert.Equal(existingInfrastructureRoleArn, props.InfrastructureRoleArn);
    }


    [Fact]
    public void ApplyECSFargateServiceDefaults_ContainerDefinitionProps_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var taskDefinition = new FargateTaskDefinition(environment.CDKStack, "TaskDef");
        var props = new ContainerDefinitionProps
        {
            TaskDefinition = taskDefinition,
            Image = ContainerImage.FromRegistry("nginx")
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults("test-project", props);

        // Assert
        Assert.NotNull(props.Logging);
    }

    [Fact]
    public void ApplyECSFargateServiceDefaults_ContainerDefinitionProps_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var taskDefinition = new FargateTaskDefinition(environment.CDKStack, "TaskDef");
        var customLogging = LogDrivers.AwsLogs(new AwsLogDriverProps
        {
            StreamPrefix = "custom-prefix"
        });

        var props = new ContainerDefinitionProps
        {
            TaskDefinition = taskDefinition,
            Image = ContainerImage.FromRegistry("nginx"),
            Logging = customLogging
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults("test-project", props);

        // Assert
        Assert.Equal(customLogging, props.Logging);
    }

    [Fact]
    public void ApplyECSFargateServiceDefaults_FargateServiceProps_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var taskDefinition = new FargateTaskDefinition(environment.CDKStack, "TaskDef");
        var props = new FargateServiceProps
        {
            TaskDefinition = taskDefinition
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(props);

        // Assert
        Assert.NotNull(props.Cluster);
        Assert.Equal(1, props.DesiredCount);
        Assert.Equal(100, props.MinHealthyPercent);
        Assert.NotNull(props.SecurityGroups);
        Assert.Single(props.SecurityGroups);
    }

    [Fact]
    public void ApplyECSFargateServiceDefaults_FargateServiceProps_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var taskDefinition = new FargateTaskDefinition(environment.CDKStack, "TaskDef");
        var customCluster = environment.DefaultsProvider.GetDefaultECSCluster();
        var existingDesiredCount = 3;
        var existingMinHealthyPercent = 50;
        var customSecurityGroups = new[] { environment.DefaultsProvider.GetDefaultECSClusterSecurityGroup() };

        var props = new FargateServiceProps
        {
            TaskDefinition = taskDefinition,
            Cluster = customCluster,
            DesiredCount = existingDesiredCount,
            MinHealthyPercent = existingMinHealthyPercent,
            SecurityGroups = customSecurityGroups
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceDefaults(props);

        // Assert
        Assert.Equal(customCluster, props.Cluster);
        Assert.Equal(existingDesiredCount, props.DesiredCount);
        Assert.Equal(existingMinHealthyPercent, props.MinHealthyPercent);
        Assert.Equal(customSecurityGroups, props.SecurityGroups);
    }

    [Fact]
    public void ApplyECSFargateServiceWithALBDefaults_ApplicationLoadBalancedTaskImageOptions_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new ApplicationLoadBalancedTaskImageOptions
        {
            Image = ContainerImage.FromRegistry("nginx")
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(props);

        // Assert
        Assert.Equal(8080, props.ContainerPort);
    }

    [Fact]
    public void ApplyECSFargateServiceWithALBDefaults_ApplicationLoadBalancedTaskImageOptions_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingContainerPort = 3000;

        var props = new ApplicationLoadBalancedTaskImageOptions
        {
            Image = ContainerImage.FromRegistry("nginx"),
            ContainerPort = existingContainerPort
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(props);

        // Assert
        Assert.Equal(existingContainerPort, props.ContainerPort);
    }

    [Fact]
    public void ApplyECSFargateServiceWithALBDefaults_ApplicationLoadBalancedFargateServiceProps_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new ApplicationLoadBalancedFargateServiceProps
        {
            TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
            {
                Image = ContainerImage.FromRegistry("nginx")
            }
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(props);

        // Assert
        Assert.NotNull(props.Cluster);
        Assert.Equal(1024, props.Cpu);
        Assert.Equal(2048, props.MemoryLimitMiB);
        Assert.Equal(2, props.DesiredCount);
        Assert.Equal(80, props.ListenerPort);
        Assert.True(props.PublicLoadBalancer);
        Assert.Equal(100, props.MinHealthyPercent);
        Assert.NotNull(props.SecurityGroups);
        Assert.Single(props.SecurityGroups);

        var expectedCluster = environment.DefaultsProvider.GetDefaultECSCluster();
        Assert.Equal(expectedCluster, props.Cluster);

        var expectedSecurityGroup = environment.DefaultsProvider.GetDefaultECSClusterSecurityGroup();
        Assert.Equal(expectedSecurityGroup, props.SecurityGroups![0]);
    }

    [Fact]
    public void ApplyECSFargateServiceWithALBDefaults_ApplicationLoadBalancedFargateServiceProps_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var customCluster = environment.DefaultsProvider.GetDefaultECSCluster();
        var existingCpu = 512;
        var existingMemory = 1024;
        var existingDesiredCount = 4;
        var existingListenerPort = 443;
        var existingPublicLoadBalancer = false;
        var existingMinHealthyPercent = 75;
        var customSecurityGroups = new[] { environment.DefaultsProvider.GetDefaultECSClusterSecurityGroup() };

        var props = new ApplicationLoadBalancedFargateServiceProps
        {
            TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
            {
                Image = ContainerImage.FromRegistry("nginx")
            },
            Cluster = customCluster,
            Cpu = existingCpu,
            MemoryLimitMiB = existingMemory,
            DesiredCount = existingDesiredCount,
            ListenerPort = existingListenerPort,
            PublicLoadBalancer = existingPublicLoadBalancer,
            MinHealthyPercent = existingMinHealthyPercent,
            SecurityGroups = customSecurityGroups
        };

        // Act
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(props);

        // Assert
        Assert.Equal(customCluster, props.Cluster);
        Assert.Equal(existingCpu, props.Cpu);
        Assert.Equal(existingMemory, props.MemoryLimitMiB);
        Assert.Equal(existingDesiredCount, props.DesiredCount);
        Assert.Equal(existingListenerPort, props.ListenerPort);
        Assert.Equal(existingPublicLoadBalancer, props.PublicLoadBalancer);
        Assert.Equal(existingMinHealthyPercent, props.MinHealthyPercent);
        Assert.Equal(customSecurityGroups, props.SecurityGroups);
    }

    [Fact]
    public void ApplyCfnReplicationGroupPropsDefaults_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new CfnReplicationGroupProps();

        // Act
        environment.DefaultsProvider.ApplyCfnReplicationGroupPropsDefaults(props);

        // Assert
        Assert.NotNull(props.ReplicationGroupDescription);
        Assert.NotNull(props.CacheNodeType);
        Assert.NotNull(props.Engine);
        Assert.NotNull(props.EngineVersion);
        Assert.NotNull(props.NumCacheClusters);
        Assert.NotNull(props.AutomaticFailoverEnabled);
        Assert.NotNull(props.Port);
        Assert.NotNull(props.CacheSubnetGroupName);
        Assert.NotNull(props.CacheParameterGroupName);
        Assert.NotNull(props.SecurityGroupIds);

        // Verify defaults match expected values
        Assert.Equal("Provision Cache for Aspire Application", props.ReplicationGroupDescription);
        Assert.Equal("cache.t3.micro", props.CacheNodeType);
        Assert.Equal("valkey", props.Engine);
        Assert.Equal("8.2", props.EngineVersion);
        Assert.Equal(2, props.NumCacheClusters);
        Assert.True((bool)props.AutomaticFailoverEnabled);
        Assert.Equal(6379, props.Port);

        var expectedSubnetGroup = environment.DefaultsProvider.GetDefaultElastiCacheCfnSubnetGroup();
        Assert.Equal(expectedSubnetGroup.Ref, props.CacheSubnetGroupName);

        var expectedSecurityGroup = environment.DefaultsProvider.GetDefaultElastiCacheProvisionClusterSecurityGroup();
        Assert.Single(props.SecurityGroupIds!);
        Assert.Equal(expectedSecurityGroup.SecurityGroupId, props.SecurityGroupIds![0]);
    }

    [Fact]
    public void ApplyCfnReplicationGroupPropsDefaults_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingDescription = "Custom description";
        var existingNodeType = "cache.t3.small";
        var existingEngine = "redis";
        var existingEngineVersion = "7.2";
        var existingNumClusters = 3.0;
        var existingAutomaticFailover = false;
        var existingPort = 6380.0;
        var existingSubnetGroup = "custom-subnet-group";
        var existingParameterGroup = "custom-parameter-group";
        var existingSecurityGroups = new[] { "sg-12345" };

        var props = new CfnReplicationGroupProps
        {
            ReplicationGroupDescription = existingDescription,
            CacheNodeType = existingNodeType,
            Engine = existingEngine,
            EngineVersion = existingEngineVersion,
            NumCacheClusters = existingNumClusters,
            AutomaticFailoverEnabled = existingAutomaticFailover,
            Port = existingPort,
            CacheSubnetGroupName = existingSubnetGroup,
            CacheParameterGroupName = existingParameterGroup,
            SecurityGroupIds = existingSecurityGroups
        };

        // Act
        environment.DefaultsProvider.ApplyCfnReplicationGroupPropsDefaults(props);

        // Assert
        Assert.Equal(existingDescription, props.ReplicationGroupDescription);
        Assert.Equal(existingNodeType, props.CacheNodeType);
        Assert.Equal(existingEngine, props.Engine);
        Assert.Equal(existingEngineVersion, props.EngineVersion);
        Assert.Equal(existingNumClusters, props.NumCacheClusters);
        Assert.Equal(existingAutomaticFailover, props.AutomaticFailoverEnabled);
        Assert.Equal(existingPort, props.Port);
        Assert.Equal(existingSubnetGroup, props.CacheSubnetGroupName);
        Assert.Equal(existingParameterGroup, props.CacheParameterGroupName);
        Assert.Equal(existingSecurityGroups, props.SecurityGroupIds);
    }

    [Fact]
    public void ApplyCfnServerlessCachePropsDefaults_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new CfnServerlessCacheProps();

        var resource = new ApplicationModel.RedisResource("cache-resource");

        // Act
        environment.DefaultsProvider.ApplyCfnServerlessCachePropsDefaults(props, resource);

        // Assert
        Assert.NotNull(props.Engine);
        Assert.NotNull(props.MajorEngineVersion);
        Assert.NotNull(props.SecurityGroupIds);
        Assert.NotNull(props.SubnetIds);

        // Verify defaults match expected values
        Assert.Equal("valkey", props.Engine);
        Assert.Equal("8", props.MajorEngineVersion);
        Assert.Equal($"{environment.CDKStack.StackName}-{resource.Name}", props.ServerlessCacheName);

        var expectedSecurityGroup = environment.DefaultsProvider.GetDefaultElastiCacheServerlessClusterSecurityGroup();
        Assert.Single(props.SecurityGroupIds!);
        Assert.Equal(expectedSecurityGroup.SecurityGroupId, props.SecurityGroupIds![0]);

        var expectedVpc = environment.DefaultsProvider.GetDefaultVpc();
        var expectedSubnets = expectedVpc.PrivateSubnets.Select(s => s.SubnetId).Take(2).ToArray();
        Assert.Equal(expectedSubnets.Length, props.SubnetIds!.Length);
    }

    [Fact]
    public void ApplyCfnServerlessCachePropsDefaults_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingEngine = "redis";
        var existingMajorVersion = "7";
        var existingDescription = "Custom serverless cache";
        var existingSecurityGroups = new[] { "sg-custom123" };
        var existingSubnets = new[] { "subnet-custom1", "subnet-custom2" };
        var existingSnapshotRetention = 7.0;
        var existingDailySnapshotTime = "03:00";


        var props = new CfnServerlessCacheProps
        {
            ServerlessCacheName = "test-cache",
            Engine = existingEngine,
            MajorEngineVersion = existingMajorVersion,
            Description = existingDescription,
            SecurityGroupIds = existingSecurityGroups,
            SubnetIds = existingSubnets,
            SnapshotRetentionLimit = existingSnapshotRetention,
            DailySnapshotTime = existingDailySnapshotTime
        };

        var resource = new ApplicationModel.RedisResource("cache-resource");

        // Act
        environment.DefaultsProvider.ApplyCfnServerlessCachePropsDefaults(props, resource);

        // Assert
        Assert.Equal(existingEngine, props.Engine);
        Assert.Equal(existingMajorVersion, props.MajorEngineVersion);
        Assert.Equal(existingDescription, props.Description);
        Assert.Equal(2, props.SecurityGroupIds!.Length); // Should contain both existing and default security group
        Assert.Contains(existingSecurityGroups[0], props.SecurityGroupIds);
        Assert.Equal(existingSubnets, props.SubnetIds);
        Assert.Equal(existingSnapshotRetention, props.SnapshotRetentionLimit);
        Assert.Equal(existingDailySnapshotTime, props.DailySnapshotTime);
    }

    [Fact]
    public void ApplyLambdaFunctionDefaults_AppliesAllDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var props = new FunctionProps
        {
            Code = Code.FromAsset("lambda-code"),
            Handler = "MyLambdaFunction::MyLambdaFunction.Function::FunctionHandler"
        };

        var lambdaProjectResource = GetLambdaProjectResource("Lambda/SQSProcessorFunction");

        // Act
        environment.DefaultsProvider.ApplyLambdaFunctionDefaults(props, lambdaProjectResource);

        // Assert
        Assert.NotNull(props.MemorySize);
        Assert.NotNull(props.Runtime);

        // Verify defaults match expected values
        Assert.Equal(512, props.MemorySize);
        Assert.Equal(Runtime.DOTNET_8, props.Runtime);
    }

    [Fact]
    public void ApplyLambdaFunctionDefaults_OverrideDefaults()
    {
        // Arrange
        var environment = CreateProviderAndEnvironment();
        var existingMemorySize = 1024;
        var existingRuntime = Runtime.DOTNET_10;
        var existingTimeout = Duration.Seconds(30);
        var existingEnvironment = new Dictionary<string, string>
        {
            { "MY_VAR", "MY_VALUE" }
        };

        var props = new FunctionProps
        {
            Code = Code.FromAsset("lambda-code"),
            Handler = "MyLambdaFunction::MyLambdaFunction.Function::FunctionHandler",
            MemorySize = existingMemorySize,
            Runtime = existingRuntime,
            Timeout = existingTimeout,
            Environment = existingEnvironment
        };

        var lambdaProjectResource = GetLambdaProjectResource("Lambda/SQSProcessorFunction");

        // Act
        environment.DefaultsProvider.ApplyLambdaFunctionDefaults(props, lambdaProjectResource);

        // Assert
        Assert.Equal(existingMemorySize, props.MemorySize);
        Assert.Equal(existingRuntime, props.Runtime);
        Assert.Equal(existingTimeout, props.Timeout);
        Assert.Equal(existingEnvironment, props.Environment);
    }

    // Helper method to create provider and environment
    private static AWSCDKEnvironmentResource<Stack> CreateProviderAndEnvironment()
    {
        var app = new App();
        var stack = new Stack(app, "TestStack");

        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environment.InitializeCDKApp(null, Path.GetTempPath());
        return environment;
    }

    public LambdaProjectResource GetLambdaProjectResource(string relativePath, string name = "TheLambdaProject")
    {
        var projectPath = GetPlaygroundProjectPath(relativePath);
        var metaDataAnnotation = new MockProjectMetadata
        {
            ProjectPath = projectPath
        };

        var lambdaProjectResource = new LambdaProjectResource(name);
        lambdaProjectResource.Annotations.Add(metaDataAnnotation);
        return lambdaProjectResource;
    }

    /// <summary>
    /// Gets the full path to a project file in the playground folder.
    /// Traverses up the directory hierarchy from the test assembly location to find the playground folder.
    /// </summary>
    /// <param name="relativePathFromPlayground">The relative path from the playground folder (e.g., "Publishing\\Publishing.AppHost\\Publishing.AppHost.csproj")</param>
    /// <returns>The full path to the file in the playground folder</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the playground folder cannot be found in the directory hierarchy</exception>
    private static string GetPlaygroundProjectPath(string relativePathFromPlayground)
    {
        // Get the directory where the test assembly is located
        var assemblyLocation = typeof(ApplyDefaultsTests).Assembly.Location;
        var testDirectory = Path.GetDirectoryName(assemblyLocation)!;

        // Traverse up until we find the playground folder
        var currentDirectory = testDirectory;
        while (currentDirectory != null)
        {
            var playgroundPath = Path.Combine(currentDirectory, "playground");
            if (Directory.Exists(playgroundPath))
            {
                // Found the playground folder, combine with relative path
                var fullPath = Path.Combine(playgroundPath, relativePathFromPlayground);

                // Verify the file/directory exists
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    var csprojPath = Directory.GetFiles(fullPath, "*.csproj").FirstOrDefault();
                    if (csprojPath == null)
                    {
                        throw new FileNotFoundException($"No .csproj file found in the directory '{fullPath}'.", fullPath);
                    }

                    return csprojPath;
                }

                throw new FileNotFoundException($"The path '{relativePathFromPlayground}' was not found in the playground folder.", fullPath);
            }

            // Move up one directory
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not find the 'playground' folder in the directory hierarchy starting from '{testDirectory}'.");
    }

    public class MockProjectMetadata : Aspire.Hosting.IProjectMetadata
    {
        public string ProjectPath { get; set; } = string.Empty;
    }
}
