// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable CA2252 // This API requires opting into preview features
#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001
#pragma warning disable ASPIREINTERACTION001

using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Constructs;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class DefaultAttributeTests
{
    [Fact]
    public void GetDefaultVpc_CreatesNewVpc_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var vpc = provider.GetDefaultVpc();

        // Assert
        Assert.NotNull(vpc);
        Assert.IsAssignableFrom<IVpc>(vpc);
    }

    [Fact]
    public void GetDefaultVpc_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultVpc>();

        // Act
        var vpc = provider.GetDefaultVpc();

        // Assert
        Assert.NotNull(vpc);
        Assert.Same(stack.CustomDefaultVpc, vpc);
    }

    [Fact]
    public void GetDefaultECSCluster_CreatesNewCluster_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var cluster = provider.GetDefaultECSCluster();

        // Assert
        Assert.NotNull(cluster);
        Assert.IsAssignableFrom<ICluster>(cluster);
    }

    [Fact]
    public void GetDefaultECSCluster_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultECSCluster>();

        // Act
        var cluster = provider.GetDefaultECSCluster();

        // Assert
        Assert.NotNull(cluster);
        Assert.Same(stack.CustomECSCluster, cluster);
    }

    [Fact]
    public void GetDefaultECSClusterSecurityGroup_CreatesNewSecurityGroup_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var securityGroup = provider.GetDefaultECSClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.IsAssignableFrom<ISecurityGroup>(securityGroup);
    }

    [Fact]
    public void GetDefaultECSClusterSecurityGroup_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultECSClusterSecurityGroup>();

        // Act
        var securityGroup = provider.GetDefaultECSClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.Same(stack.CustomECSClusterSecurityGroup, securityGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheCfnSubnetGroup_CreatesNewSubnetGroup_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var subnetGroup = provider.GetDefaultElastiCacheCfnSubnetGroup();

        // Assert
        Assert.NotNull(subnetGroup);
        Assert.IsAssignableFrom<CfnSubnetGroup>(subnetGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheCfnSubnetGroup_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultElastiCacheSubnetGroup>();

        // Act
        var subnetGroup = provider.GetDefaultElastiCacheCfnSubnetGroup();

        // Assert
        Assert.NotNull(subnetGroup);
        Assert.Same(stack.CustomSubnetGroup, subnetGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheProvisionClusterSecurityGroup_CreatesNewSecurityGroup_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var securityGroup = provider.GetDefaultElastiCacheProvisionClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.IsAssignableFrom<ISecurityGroup>(securityGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheProvisionClusterSecurityGroup_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultElastiCacheNodeSecurityGroup>();

        // Act
        var securityGroup = provider.GetDefaultElastiCacheProvisionClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.Same(stack.CustomNodeSecurityGroup, securityGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheServerlessClusterSecurityGroup_CreatesNewSecurityGroup_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var securityGroup = provider.GetDefaultElastiCacheServerlessClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.IsAssignableFrom<ISecurityGroup>(securityGroup);
    }

    [Fact]
    public void GetDefaultElastiCacheServerlessClusterSecurityGroup_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultElastiCacheServerlessSecurityGroup>();

        // Act
        var securityGroup = provider.GetDefaultElastiCacheServerlessClusterSecurityGroup();

        // Assert
        Assert.NotNull(securityGroup);
        Assert.Same(stack.CustomServerlessSecurityGroup, securityGroup);
    }

    [Fact]
    public void GetDefaultECSExpressExecutionRole_CreatesNewRole_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var role = provider.GetDefaultECSExpressExecutionRole();

        // Assert
        Assert.NotNull(role);
        Assert.IsAssignableFrom<IRole>(role);
    }

    [Fact]
    public void GetDefaultECSExpressExecutionRole_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultECSExpressExecutionRole>();

        // Act
        var role = provider.GetDefaultECSExpressExecutionRole();

        // Assert
        Assert.NotNull(role);
        Assert.Same(stack.CustomExecutionRole, role);
    }

    [Fact]
    public void GetDefaultECSExpressInfrastructureRole_CreatesNewRole_WhenNoAttributeFound()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var role = provider.GetDefaultECSExpressInfrastructureRole();

        // Assert
        Assert.NotNull(role);
        Assert.IsAssignableFrom<IRole>(role);
    }

    [Fact]
    public void GetDefaultECSExpressInfrastructureRole_UsesAttributedProperty_WhenAttributeFound()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultECSExpressInfrastructureRole>();

        // Act
        var role = provider.GetDefaultECSExpressInfrastructureRole();

        // Assert
        Assert.NotNull(role);
        Assert.Same(stack.CustomInfrastructureRole, role);
    }

    [Fact]
    public void GetDefaultConstructs_AreCached_AfterFirstCall()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<EmptyStack>();

        // Act
        var vpc1 = provider.GetDefaultVpc();
        var vpc2 = provider.GetDefaultVpc();

        var cluster1 = provider.GetDefaultECSCluster();
        var cluster2 = provider.GetDefaultECSCluster();

        // Assert
        Assert.Same(vpc1, vpc2);
        Assert.Same(cluster1, cluster2);
    }

    [Fact]
    public void GetDefaultVpc_ThrowsException_WhenAttributedPropertyHasWrongType()
    {
        // Arrange
        var (provider, _) = CreateProviderWithStack<StackWithWrongTypeForDefaultVpc>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetDefaultVpc());
        Assert.Contains("is not of type", exception.Message);
    }

    [Fact]
    public void GetDefaultVpc_UsesAttributedField_WhenAttributeFoundOnField()
    {
        // Arrange
        var (provider, stack) = CreateProviderWithStack<StackWithDefaultVpcField>();

        // Act
        var vpc = provider.GetDefaultVpc();

        // Assert
        Assert.NotNull(vpc);
        Assert.Same(stack.CustomDefaultVpcField, vpc);
    }

    // Helper method to create provider with a custom stack
    private static (CDKDefaultsProviderPreviewV1 Provider, T Stack) CreateProviderWithStack<T>() where T : Stack
    {
        var app = new App();
        var stack = (T)Activator.CreateInstance(typeof(T), app, "TestStack", null)!;
        
        var environmentResource = new AWSCDKEnvironmentResource<T>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (a, props) => stack,
            null);

        environmentResource.InitializeCDKApp(null, Path.GetTempPath());
        var provider = new CDKDefaultsProviderPreviewV1(environmentResource);
        
        return (provider, stack);
    }

    // Test stack classes
    private class EmptyStack : Stack
    {
        public EmptyStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
        }
    }

    private class StackWithDefaultVpc : Stack
    {
        public StackWithDefaultVpc(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpc = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
        }

        [DefaultVpc]
        public IVpc CustomDefaultVpc { get; }
    }

    private class StackWithDefaultVpcField : Stack
    {
        [DefaultVpc]
        public readonly IVpc CustomDefaultVpcField;

        public StackWithDefaultVpcField(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpcField = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
        }
    }

    private class StackWithDefaultECSCluster : Stack
    {
        public StackWithDefaultECSCluster(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpc = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
            CustomECSCluster = new Cluster(this, "CustomCluster", new ClusterProps { Vpc = CustomDefaultVpc });
        }

        [DefaultVpc]
        public IVpc CustomDefaultVpc { get; }

        [DefaultECSCluster]
        public ICluster CustomECSCluster { get; }
    }

    private class StackWithDefaultECSClusterSecurityGroup : Stack
    {
        public StackWithDefaultECSClusterSecurityGroup(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpc = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
            CustomECSClusterSecurityGroup = new SecurityGroup(this, "CustomSG", new SecurityGroupProps { Vpc = CustomDefaultVpc });
        }

        [DefaultVpc]
        public IVpc CustomDefaultVpc { get; }

        [DefaultECSClusterSecurityGroup]
        public ISecurityGroup CustomECSClusterSecurityGroup { get; }
    }

    private class StackWithDefaultElastiCacheSubnetGroup : Stack
    {
        public StackWithDefaultElastiCacheSubnetGroup(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomSubnetGroup = new CfnSubnetGroup(this, "CustomSubnetGroup", new CfnSubnetGroupProps
            {
                Description = "Custom subnet group",
                SubnetIds = new[] { "subnet-12345", "subnet-67890" }
            });
        }

        [DefaultElastiCacheCfnSubnetGroup]
        public CfnSubnetGroup CustomSubnetGroup { get; }
    }

    private class StackWithDefaultElastiCacheNodeSecurityGroup : Stack
    {
        public StackWithDefaultElastiCacheNodeSecurityGroup(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpc = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
            CustomNodeSecurityGroup = new SecurityGroup(this, "CustomNodeSG", new SecurityGroupProps { Vpc = CustomDefaultVpc });
        }

        [DefaultVpc]
        public IVpc CustomDefaultVpc { get; }

        [DefaultElastiCacheNodeSecurityGroup]
        public ISecurityGroup CustomNodeSecurityGroup { get; }
    }

    private class StackWithDefaultElastiCacheServerlessSecurityGroup : Stack
    {
        public StackWithDefaultElastiCacheServerlessSecurityGroup(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomDefaultVpc = new Vpc(this, "CustomDefaultVpc", new VpcProps
            {
                MaxAzs = 2
            });
            CustomServerlessSecurityGroup = new SecurityGroup(this, "CustomServerlessSG", new SecurityGroupProps { Vpc = CustomDefaultVpc });
        }

        [DefaultVpc]
        public IVpc CustomDefaultVpc { get; }

        [DefaultElastiCacheServerlessSecurityGroup]
        public ISecurityGroup CustomServerlessSecurityGroup { get; }
    }

    private class StackWithDefaultECSExpressExecutionRole : Stack
    {
        public StackWithDefaultECSExpressExecutionRole(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomExecutionRole = new Role(this, "CustomExecutionRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
            });
        }

        [DefaultECSExpressExecutionRole]
        public IRole CustomExecutionRole { get; }
    }

    private class StackWithDefaultECSExpressInfrastructureRole : Stack
    {
        public StackWithDefaultECSExpressInfrastructureRole(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            CustomInfrastructureRole = new Role(this, "CustomInfraRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ecs.amazonaws.com")
            });
        }

        [DefaultECSExpressInfrastructureRole]
        public IRole CustomInfrastructureRole { get; }
    }

    private class StackWithWrongTypeForDefaultVpc : Stack
    {
        public StackWithWrongTypeForDefaultVpc(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            WrongType = "This is a string, not an IVpc";
        }

        [DefaultVpc]
        public string WrongType { get; }
    }
}
