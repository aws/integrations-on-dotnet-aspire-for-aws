# AWS Deployment Design for .NET Aspire

## Overview

The AWS deployment system for .NET Aspire transforms Aspire AppHost resources into AWS CDK constructs, which are then synthesized into CloudFormation templates and deployed to AWS. This document describes the architecture and design patterns used in the deployment system.

## Table of Contents

1. [Adding an AWS CDK Environment](#adding-an-aws-cdk-environment)
2. [Custom CDK Stacks](#custom-cdk-stacks)
3. [Publish Extension Methods](#publish-extension-methods)
4. [CDK Props and Construct Callbacks](#cdk-props-and-construct-callbacks)
5. [Default Constructs and Attributes](#default-constructs-and-attributes)
6. [CDKDefaultsProvider System](#cdkdefaultsprovider-system)
7. [Publish Target Selection](#publish-target-selection)
8. [Resource References and Connectivity](#resource-references-and-connectivity)
9. [Adding New Publish Targets](#adding-new-publish-targets)

---

## Adding an AWS CDK Environment

The deployment process begins by adding an AWS CDK environment to your Aspire AppHost using the `AddAWSCDKEnvironment` extension method:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1,
    stackName: "MyStack"
);
```

### Key Components

- **Name**: Used as both the Aspire resource name and the CloudFormation stack name
- **CDKDefaultsProviderFactory**: Configures default values and behaviors for AWS resources
- **AWSCDKEnvironmentResourceConfig** (optional): Additional configuration like AWS SDK settings
- **StackName** (optional): The name of the AWS CloudFormation stack that will be created. If not set the resource name will be used.

### Prerequisites


- Node.js 22.x must be installed
- AWS CDK must be installed globally (`npm install -g aws-cdk`)
- AWS CDK bootstrap must be run on the target account and region

### How It Works

When you add an AWS CDK environment:

1. **Service Registration**: The environment registers all necessary services including:
   - `IAWSEnvironmentService`: Abstracts access environment properties like the current command line arguments. Having the service allows mocking over `System.Environment`.
   - `CDKPublishingStep`: Handles the CDK synthesis process
   - `CDKDeployStep`: Executes the CDK deployment
   - Various implementations of `IAWSPublishTarget` that are the publish target for Aspire resources to CDK constructs.

2. **Pipeline Integration**: The environment adds two pipeline steps:
   - **Publish Step**: Transforms Aspire resources into CDK constructs, sets up connections between constructs based on Aspire `WithReference` calls and synthesizes CloudFormation templates
   - **Deploy Step**: Executes `cdk deploy` to provision resources in AWS

---

## Custom CDK Stacks

You can provide your own custom CDK Stack to define additional resources or override default constructs:

```csharp
public class DeploymentStack : Stack
{
    public DeploymentStack(Construct scope, string id, IStackProps? props = null) 
        : base(scope, id, props)
    {
        // Use the account's default VPC instead of creating a new one
        DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
        {
            IsDefault = true
        });
        
        // Create a custom ECS cluster with specific configuration
        DefaultECSCluster = new Cluster(this, "MyCluster", new ClusterProps
        {
            Vpc = DefaultVpc,
            ClusterName = "my-aspire-cluster"
        });
    }

    [DefaultVpc]
    public IVpc DefaultVpc { get; private set; }
    
    [DefaultECSCluster]
    public ICluster DefaultECSCluster { get; private set; }
}
```

The `DefaultVpc` and `DefaultECSCluster` attributes are part of the AWS Aspire integration that
identify during the publishing what constructs to use when referencing resources like VPC and ECS Clusters. See the [Default Constructs and Attributes](#default-constructs-and-attributes) section for further details about the attributes.


### Using the Custom Stack

```csharp
builder.AddAWSCDKEnvironment<DeploymentStack>(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1,
    stackFactory: (app, props) => new DeploymentStack(app, "MyApp", props)
);
```

### Benefits

- Define infrastructure resources alongside your Aspire resources
- Override default constructs (VPC, ECS Cluster, Security Groups, etc.)
- Integrate with existing AWS infrastructure
- Apply custom tags, policies, or configurations

---

## Publish Extension Methods

By default, Aspire resources are mapped to AWS services based on their type. You can override this behavior using Publish extension methods:

### Overriding Defaults

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Override the default ECS Fargate Express Service to use ECS Fargate with an Application Loadbalancer.
builder.AddProject<Projects.WebApp>("webapp")
        .PublishAsECSFargateServiceWithALB();
```

## CDK Props and Construct Callbacks

Each Publish method accepts a configuration object that provides callbacks to customize the CDK props and constructs:

### Props Callbacks

Modify the properties before the CDK construct is created:

```csharp
builder.AddProject<Projects.WebApp>("webapp")
        .PublishAsECSFargateServiceWithALB(new PublishECSFargateServiceWithALBConfig
        {
            PropsApplicationLoadBalancedTaskImageOptionsCallback = (ctx, props) =>
            {
                // Change the container port from default 8080 to 5000
                props.ContainerPort = 5000;
            },
            
            PropsApplicationLoadBalancedFargateServiceCallback = (ctx, props) =>
            {
                // Configure the load balancer to use HTTPS
                props.Protocol = ApplicationProtocol.HTTPS;
                props.Certificate = Certificate.FromCertificateArn(
                    ctx.Stack, 
                    "Cert", 
                    "arn:aws:acm:us-east-1:123456789012:certificate/abc123"
                );
            }
        });
```

### Construct Callbacks

Modify the CDK construct after it's created:

```csharp
lambdaFunction.PublishAsLambdaFunction(new PublishLambdaFunctionConfig
{
    ConstructFunctionCallback = (ctx, construct) =>
    {
        // Add an SQS event source to the Lambda function
        var queue = ctx.GetDeploymentStack<DeploymentStack>().MyQueue;
        construct.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps
        {
            BatchSize = 5,
            Enabled = true
        }));
        
        // Grant the function permissions to access other AWS resources
        myBucket.GrantReadWrite(construct);
    }
});
```

### CDKPublishTargetContext

The context object passed to callbacks provides:

- **`Stack`**: The CDK Stack being deployed to
- **`DefaultsProvider`**: Access to default constructs and values
- **`GetDeploymentStack<T>()`**: Type-safe access to your custom stack

---

## Default Constructs and Attributes

The deployment system creates default shared constructs (VPC, ECS Cluster, Security Groups, etc.) that are reused across multiple resources. You can override these defaults using attributes in your custom stack. For example adding the `DefaultVpcAttribute` on a property of type `IVpc` in the CDK Stack.

### How It Works

1. **Attribute Discovery**: When a default construct is needed, the system uses reflection to search for properties/fields marked with the corresponding attribute in your custom stack

2. **Fallback Creation**: If no attribute is found, the `CDKDefaultsProvider` creates a default construct using its `CreateDefault*()` methods

3. **Singleton Pattern**: Default constructs are created once and reused across all resources for the parent AWS CDK environment.

### Example: Using Account's Default VPC

```csharp
public class DeploymentStack : Stack
{
    public DeploymentStack(Construct scope, string id, IStackProps? props = null) 
        : base(scope, id, props)
    {
        // Look up the account's default VPC instead of creating a new one
        DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
        {
            IsDefault = true
        });
    }

    [DefaultVpc]
    public IVpc DefaultVpc { get; private set; }
}
```

Now all resources requiring a VPC will use the account's default VPC instead of creating a new one.


### Default Construct Creation

If you don't override a default construct, the `CDKDefaultsProvider` creates sensible defaults:

**Default VPC**:
```csharp
protected virtual IVpc CreateDefaultVpc()
{
    return new Vpc(EnvironmentResource.CDKStack, "DefaultVPC", new VpcProps
    {
        MaxAzs = 2  // Creates VPC with 2 availability zones
    });
}
```

**Default ECS Cluster**:
```csharp
protected virtual ICluster CreateDefaultECSCluster()
{
    return new Cluster(EnvironmentResource.CDKStack, "DefaultECSCluster", new ClusterProps
    {
        Vpc = GetDefaultVpc()
    });
}
```

---

## CDKDefaultsProvider System

The `CDKDefaultsProvider` is a versioned system that provides default values and behaviors for AWS resources. This allows the library to evolve while giving users control over when to adopt breaking changes. 

For example today the default engine type version for ElastiCache cluster is `8`. At some point there will be a newer version we should default to. If we change the version to `9` for example that could cause CloudFormation to decide to replace the ElastiCache cluster which could be undesirable. With the CDKDefaultsProvider we will create a new version letting users choose when and if they want to opt-in to the new defaults.

### Purpose

1. **Versioned Defaults**: Lock in a specific set of default behaviors
2. **Opt-in Breaking Changes**: Users choose when to upgrade to new versions
3. **Customization**: Users can extend providers to override specific defaults

### Version Hierarchy

```
CDKDefaultsProvider (abstract base)
    ↓
CDKDefaultsProviderPreviewV1 (current version)
    ↓
Your Custom Provider (optional)
```

### Using a Defaults Provider

```csharp
// Use the latest preview version
builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1
);
```

### What the Provider Controls

#### 1. Default Publish Targets


```csharp
public virtual WebProjectResourcePublishTarget DefaultWebProjectResourcePublishTarget 
    { get; set; } = WebProjectResourcePublishTarget.ECSFargateExpressService;

public virtual ConsoleProjectResourcePublishTarget DefaultConsoleProjectResourcePublishTarget 
    { get; set; } = ConsoleProjectResourcePublishTarget.ECSFargateService;

public virtual LambdaProjectResourcePublishTarget DefaultLambdaProjectResourcePublishTarget 
    { get; set; } = LambdaProjectResourcePublishTarget.LambdaFunction;

public virtual RedisResourcePublishTarget DefaultRedisResourcePublishTarget 
    { get; set; } = RedisResourcePublishTarget.ElastiCacheServerlessCluster;
```

#### 2. Resource-Specific Defaults

Each resource type has an `Apply*Defaults()` method that sets default values:

```csharp
// Example: Lambda function defaults
public virtual void ApplyLambdaFunctionDefaults(FunctionProps props, LambdaProjectResource resource)
{
    props.Runtime ??= Runtime.DOTNET_8;
    props.MemorySize ??= 512;
    props.Timeout ??= Duration.Seconds(30);
    props.Architecture ??= Architecture.X86_64;
}

// Example: ECS Express service defaults
public virtual void ApplyCfnExpressGatewayServiceDefaults(CfnExpressGatewayServiceProps props)
{
    props.ExecutionRole ??= GetDefaultECSExpressExecutionRole().RoleArn;
    props.InfrastructureRole ??= GetDefaultECSExpressInfrastructureRole().RoleArn;
    
    if (props.PrimaryContainer is ExpressGatewayContainerProperty container)
    {
        container.Port ??= 8080;  // Default container port
    }
}
```

#### 3. Default Construct Creation

As shown earlier, the provider controls how default constructs are created when not overridden by attributes.

### Creating a Custom Provider

You can extend an existing provider to customize specific defaults:

```csharp
public class MyCustomProvider : CDKDefaultsProviderPreviewV1
{
    public MyCustomProvider(AWSCDKEnvironmentResource environmentResource) 
        : base(environmentResource)
    {
        // Change default for web apps to use ALB instead of Express
        DefaultWebProjectResourcePublishTarget = 
            WebProjectResourcePublishTarget.ECSFargateServiceWithALB;
    }
    
    // Override Lambda defaults to use ARM architecture
    public override void ApplyLambdaFunctionDefaults(FunctionProps props, LambdaProjectResource resource)
    {
        base.ApplyLambdaFunctionDefaults(props, resource);
        props.Architecture = Architecture.ARM_64;
        props.MemorySize = 1024;  // Use more memory for ARM
    }
    
    // Override ElastiCache node type
    protected override string ElasticCacheNodeClusterNodeType => "cache.t4.micro";
}

// Use your custom provider
var customFactory = new CDKDefaultsProviderFactory(env => new MyCustomProvider(env));
builder.AddAWSCDKEnvironment("MyApp", customFactory);
```

### Future Versions and Breaking Changes

When the library needs to make breaking changes to defaults (e.g., changing ElastiCache node type from `cache.t3.micro` to `cache.t4.micro`), a new version will be released:

```csharp
// Future version with breaking changes
public class CDKDefaultsProviderV2 : CDKDefaultsProvider
{
    protected override string ElasticCacheNodeClusterNodeType => "cache.t4.micro";
    
    public override WebProjectResourcePublishTarget DefaultWebProjectResourcePublishTarget 
        { get; set; } = WebProjectResourcePublishTarget.ECSFargateServiceWithALB;
}

// Users opt-in when ready
CDKDefaultsProviderFactory.V2
```

This allows users to:
- Stay on Preview_V1 to avoid breaking changes
- Upgrade to V2 when ready to adopt new defaults
- Create custom providers that cherry-pick changes from both versions

When creating new CDKDefaultsProvider versions the comment block for the new class as well as the new `CDKDefaultsProviderFactory`
static property for the new provider must contain the list of breaking changes from the previous version.

---

## Publish Target Selection

The system determines which AWS service to use for each Aspire resource through a multi-step process:

### Selection Process

1. **Explicit Annotation**: Check if the resource has an `IAWSPublishTargetAnnotation` applied via a Publish extension method
2. **Default Target Matching**: If no explicit annotation, query all registered publish targets to find the best match
3. **Ranking**: If multiple targets match, select the one with the highest rank


### Step 1: Explicit Annotation

When you call a Publish extension method, it adds an `IAWSPublishTargetAnnotation` to the resource:

```csharp
builder.AddProject<Projects.WebApp>("webapp")
    .PublishAsECSFargateServiceWithALB();  // Adds PublishECSFargateServiceWithALBAnnotation
```

During the publish step, the system checks for this annotation:

```csharp
if (resource.TryGetLastAnnotation<IAWSPublishTargetAnnotation>(out var publishAnnotation))
{
    // Use the explicitly specified publish target
    var publishTarget = _annotationsToPublishTargetsMapping[publishAnnotation.GetType()];
    await publishTarget.GenerateConstructAsync(environment, resource, publishAnnotation, cancellationToken);
}
```

### Step 2: Default Target Matching

If no explicit annotation exists, the system queries each registered publish target:

```csharp
private IResourceAnnotation? DetermineDefaultPublishAnnotation(
    AWSCDKEnvironmentResource environment, 
    IResource resource)
{
    IsDefaultPublishTargetMatchResult? bestMatch = null;
    
    foreach(var publishTarget in _annotationsToPublishTargetsMapping.Values)
    {
        var matchResults = publishTarget.IsDefaultPublishTargetMatch(
            environment.DefaultsProvider, 
            resource
        );

        if (matchResults.IsMatch && (bestMatch == null || bestMatch.Rank < matchResults.Rank))
        {
            bestMatch = matchResults;
        }
    }

    return bestMatch?.PublishTargetAnnotation;
}
```

### Step 3: IsDefaultPublishTargetMatch Implementation

Each publish target implements `IsDefaultPublishTargetMatch()` to determine if it's a good fit:

**Example: ECS Fargate Express Service**
```csharp
public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(
    CDKDefaultsProvider cdkDefaultsProvider, 
    IResource resource)
{
    if (resource is ProjectResource projectResource &&
        projectResource.GetEndpoints().Any() &&  // Has HTTP endpoints
        cdkDefaultsProvider.DefaultWebProjectResourcePublishTarget == 
            CDKDefaultsProvider.WebProjectResourcePublishTarget.ECSFargateExpressService)
    {
        return new IsDefaultPublishTargetMatchResult
        {
            IsMatch = true,
            PublishTargetAnnotation = new PublishECSFargateServiceExpressAnnotation(),
            Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100
        };
    }

    return IsDefaultPublishTargetMatchResult.NO_MATCH;
}
```


**Example: Lambda Function**
```csharp
public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(
    CDKDefaultsProvider cdkDefaultsProvider, 
    IResource resource)
{
    if (resource is LambdaProjectResource &&
        cdkDefaultsProvider.DefaultLambdaProjectResourcePublishTarget == 
            CDKDefaultsProvider.LambdaProjectResourcePublishTarget.LambdaFunction)
    {
        return new IsDefaultPublishTargetMatchResult
        {
            IsMatch = true,
            PublishTargetAnnotation = new PublishLambdaFunctionAnnotation(),
            Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 200  // Higher rank
        };
    }

    return IsDefaultPublishTargetMatchResult.NO_MATCH;
}
```

### Ranking System

The rank determines priority when multiple targets match. An example of a scenario where there can be multiple matches is an Aspire `ProjectResource` that has endpoints (i.e. Web Project). The publishing target for web a web project match as well as the generic .NET project. The rank allows the system to prefer the web project match over the generic .NET project.


Higher ranks win. This ensures that more specific resource types (like LambdaProjectResource) take precedence over generic types (like ProjectResource).

### Default Target Properties

The `CDKDefaultsProvider` exposes properties that control default target selection:

```csharp
public virtual WebProjectResourcePublishTarget DefaultWebProjectResourcePublishTarget 
    { get; set; } = WebProjectResourcePublishTarget.ECSFargateExpressService;
```

Publish targets check these properties in their `IsDefaultPublishTargetMatch()` implementation, allowing users to change defaults by customizing the provider.

---

## Resource References and Connectivity

When Aspire resources reference each other, the deployment system automatically configures connectivity through environment variables, security groups, and VPC attachments.

### How References Work

```csharp
var cache = builder.AddRedis("cache");

var webApp = builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(cache);  // Creates a reference

var lambdaFunction = builder.AddAWSLambdaFunction<Projects.MyFunction>("function", "handler")
    .WithReference(cache);  // Lambda will be attached to VPC
```


### Reference Processing

During CDK construct generation, the system processes relationships:

```csharp
protected virtual void ProcessRelationShips(
    AbstractCDKConstructConnectionPoints referencePoints, 
    IResource resource)
{
    var allLinkReferences = GetAllReferencesLinks(resource);
    
    foreach (var linkAnnotation in allLinkReferences)
    {
        // 1. Get connection info (environment variables)
        var results = linkAnnotation.PublishTarget.GetReferenceConnectionInfo(linkAnnotation);
        if (results.EnvironmentVariables != null)
        {
            foreach (var kvp in results.EnvironmentVariables)
                referencePoints.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        // 2. Check if VPC attachment is required
        if (linkAnnotation.PublishTarget.ReferenceRequiresVPC())
        {
            referencePoints.Vpc = linkAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultVpc();
        }

        // 3. Check if security group ingress is required
        if (linkAnnotation.PublishTarget.ReferenceRequiresSecurityGroup() && 
            referencePoints.ReferenceSecurityGroup != null)
        {
            linkAnnotation.PublishTarget.ApplyReferenceSecurityGroup(
                linkAnnotation, 
                referencePoints.ReferenceSecurityGroup
            );
        }
    }
}
```

### 1. Environment Variables

Each publish target implements `GetReferenceConnectionInfo()` to provide connection details:

**Example: ECS Express Service**
```csharp
public override ReferenceConnectionInfo GetReferenceConnectionInfo(
    AWSLinkedObjectsAnnotation linkedAnnotation)
{
    var result = new ReferenceConnectionInfo();
    if (linkedAnnotation.Construct is not CfnExpressGatewayService fargateExpressConstruct)
        return result;

    result.EnvironmentVariables = new Dictionary<string, string>();
    
    // Add the HTTPS endpoint as an environment variable
    var key = $"services__{linkedAnnotation.Resource.Name}__https__0";
    var endpoint = Fn.Join("", [
        "https://", 
        Fn.GetAtt(fargateExpressConstruct.LogicalId, "Endpoint").ToString(), 
        "/"
    ]);
    result.EnvironmentVariables[key] = endpoint;

    return result;
}
```


**Example: ElastiCache Cluster**
```csharp
public override ReferenceConnectionInfo GetReferenceConnectionInfo(
    AWSLinkedObjectsAnnotation linkedAnnotation)
{
    var result = new ReferenceConnectionInfo();
    if (linkedAnnotation.Construct is not CfnReplicationGroup replicationGroup)
        return result;

    result.EnvironmentVariables = new Dictionary<string, string>();
    
    // Add the Redis connection string
    var connectionString = Fn.Join("", [
        Fn.GetAtt(replicationGroup.LogicalId, "PrimaryEndPoint.Address").ToString(),
        ":",
        Fn.GetAtt(replicationGroup.LogicalId, "PrimaryEndPoint.Port").ToString()
    ]);
    
    result.EnvironmentVariables[$"ConnectionStrings__{linkedAnnotation.Resource.Name}"] = 
        connectionString;

    return result;
}
```

### 2. VPC Attachment

Some resources (like ElastiCache) require the referencing resource to be in the same VPC. The `ReferenceRequiresVPC()` method indicates this:

**ElastiCache Publish Target**
```csharp
public override bool ReferenceRequiresVPC()
{
    return true;  // Resources connecting to ElastiCache must be in VPC
}
```

**Lambda Function Connection Points**
```csharp
public override IVpc? Vpc
{
    get => props.Vpc;
    set
    {
        if (!value?.PrivateSubnets?.Any() ?? false && 
            props.AllowPublicSubnet.GetValueOrDefault() == false)
        {
            throw new InvalidOperationException(
                "Lambda function references a resource that requires VPC attachment. " +
                "The configured VPC contains only public subnets. " +
                "Lambda functions must be attached to private subnets."
            );
        }
        props.Vpc = value;
    }
}
```

### Lambda VPC Attachment Behavior

Lambda functions are **only attached to a VPC if a reference requires it**:

- **No VPC-requiring references**: Lambda runs without VPC attachment (can access internet and AWS APIs)
- **Has VPC-requiring references**: Lambda is attached to VPC's private subnets (requires NAT Gateway for internet access)

This is determined by the `ReferenceRequiresVPC()` method of the referenced resource's publish target.


### 3. Security Group Ingress Rules

Resources in a VPC use security groups to control network access. When one resource references another, security group ingress rules are created:

**ElastiCache Publish Target**
```csharp
public override bool ReferenceRequiresSecurityGroup()
{
    return true;  // Need security group ingress for network access
}

public override void ApplyReferenceSecurityGroup(
    AWSLinkedObjectsAnnotation linkedAnnotation, 
    ISecurityGroup securityGroup)
{
    if (linkedAnnotation.Construct is not CfnReplicationGroup replicationGroup)
        return;

    var elastiCacheSecurityGroup = 
        linkedAnnotation.EnvironmentResource.DefaultsProvider
            .GetDefaultElastiCacheProvisionClusterSecurityGroup();

    // Add ingress rule allowing the referencing resource's security group
    elastiCacheSecurityGroup.AddIngressRule(
        securityGroup,
        Port.Tcp(6379),  // Redis port
        "Allow access from referencing resource"
    );
}
```

### Connection Points Pattern

Each CDK construct type has a corresponding "Connection Points" class that provides a uniform interface for setting environment variables, VPC, and security groups:

```csharp
public class AbstractCDKConstructConnectionPoints
{
    public virtual IDictionary<string, string>? EnvironmentVariables { get; set; }
    public virtual ISecurityGroup? ReferenceSecurityGroup { get; }
    public virtual IVpc? Vpc { get; set; }
}
```

**Example: Lambda Function Connection Points**
```csharp
internal class FunctionPropsConnectionPoints : AbstractCDKConstructConnectionPoints
{
    private FunctionProps props;
    private Func<ISecurityGroup> securityGroupFactory;
    private ISecurityGroup? _referenceSecurityGroup;

    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.Environment ?? new Dictionary<string, string>();
        set => props.Environment = value ?? new Dictionary<string, string>();
    }

    public override ISecurityGroup? ReferenceSecurityGroup
    {
        get
        {
            // Lazy creation: only create security group if needed
            _referenceSecurityGroup ??= securityGroupFactory();
            return _referenceSecurityGroup;
        }
    }

    public override IVpc? Vpc
    {
        get => props.Vpc;
        set => props.Vpc = value;
    }
}
```


### Reference Security Group Creation

For resources that need to reference VPC-attached resources, an empty security group is created:

```csharp
protected ISecurityGroup CreateEmptyReferenceSecurityGroup<T>(
    AWSCDKEnvironmentResource environmentResource, 
    IResource resource, 
    T construct, 
    Func<T, ISecurityGroup[]?> getter, 
    Action<T, ISecurityGroup[]> setter)
{
    var securityGroup = new SecurityGroup(
        environmentResource.CDKStack,
        $"{resource.Name}-Ref",
        new SecurityGroupProps
        {
            Vpc = environmentResource.DefaultsProvider.GetDefaultVpc(),
            Description = $"Security group for linking {resource.Name} to Aspire References",
            AllowAllOutbound = true
        });
    
    // Add to the construct's security groups
    AppendSecurityGroup(construct, getter, setter, securityGroup);
    
    return securityGroup;
}
```

This security group is then added as an ingress rule to the referenced resource's security group, enabling network connectivity.

---

## Adding New Publish Targets

There are two scenarios for adding publish target support:

1. **New Aspire Resource Type**: Adding the first AWS publish target for a resource type
2. **Alternative Publish Target**: Adding another deployment option for an existing resource type

### Scenario 1: New Aspire Resource Type

Let's add support for publishing a hypothetical `PostgresResource` to Amazon RDS that will use the `CfnDBInstance` CDK construct.
(This is an approximate implementation for example purposes)

#### Step 1: Create the Annotation

In a file named with the Annotations name in this case `PublishRDSPostgresAnnotation.cs` add both the
annotation and config object. On the annotation has a property to set the config. The config object
contains `PublishCallback` for each CDK construct and construct prop created. Some publishing targets like
the ECS Fargate Service create multiple constructs. In that case the config will have separate 
props and construct callback for each resource.

```csharp
// Internal annotation class
internal class PublishRDSPostgresAnnotation : IAWSPublishTargetAnnotation
{
    public PublishRDSPostgresConfig Config { get; set; } = new();
}

// Public configuration class
public class PublishRDSPostgresConfig
{
    public PublishCallback<CfnDBInstanceProps>? PropsCfnDBInstanceCallback { get; set; }
    public PublishCallback<CfnDBInstance>? ConstructCfnDBInstanceCallback { get; set; }
}
```

#### Step 2: Create the Publish Target Implementation

```csharp
internal class RDSPostgresPublishTarget : AbstractAWSPublishTarget
{
    public RDSPostgresPublishTarget(ILogger<RDSPostgresPublishTarget> logger) 
        : base(logger) { }

    public override string PublishTargetName => "RDS PostgreSQL Database";

    public override Type PublishTargetAnnotation => typeof(PublishRDSPostgresAnnotation);

    public override async Task GenerateConstructAsync(
        AWSCDKEnvironmentResource environment, 
        IResource resource, 
        IAWSPublishTargetAnnotation annotation, 
        CancellationToken cancellationToken)
    {
        var postgresResource = resource as PostgresResource
            ?? throw new InvalidOperationException($"Resource {resource.Name} is not a PostgresResource.");

        var publishAnnotation = annotation as PublishRDSPostgresAnnotation
            ?? throw new InvalidOperationException($"Annotation is not a PublishRDSPostgresAnnotation.");

        // Create the RDS instance props
        var instanceProps = new DCfnDBInstanceProps
        {
            Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps
            {
                Version = PostgresEngineVersion.VER_15
            }),
            InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MICRO),
            Vpc = environment.DefaultsProvider.GetDefaultVpc(),
            DatabaseName = postgresResource.Name
        };

        // Apply user callbacks
        publishAnnotation.Config.PropsCfnDBInstanceCallback?.Invoke(
            CreatePublishTargetContext(environment), 
            instanceProps
        );

        // Apply defaults from provider
        environment.DefaultsProvider.ApplyRDSPostgresDefaults(instanceProps, postgresResource);

        // Create the construct
        var dbInstance = new CfnDBInstance(
            environment.CDKStack, 
            $"RDS-{postgresResource.Name}", 
            instanceProps
        );

        // Apply construct callback
        publishAnnotation.Config.ConstructCfnDBInstanceCallback?.Invoke(
            CreatePublishTargetContext(environment), 
            dbInstance
        );

        // Link the Aspire resource to the CDK construct
        ApplyAWSLinkedObjectsAnnotation(environment, postgresResource, dbInstance, this);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(
        CDKDefaultsProvider cdkDefaultsProvider, 
        IResource resource)
    {
        // Check if this is a PostgresResource and the provider says to use RDS
        if (resource is PostgresResource &&
            cdkDefaultsProvider.DefaultPostgresResourcePublishTarget == 
                CDKDefaultsProvider.PostgresResourcePublishTarget.RDSPostgres)
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishRDSPostgresAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(
        AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not DatabaseInstance dbInstance)
            return result;

        // NOTE: In the real version when we add Database support will
        // use AWS Secrets Manager instead of environment variables
        // for the connection string. This method is shown here for example 
        // purposes.
        result.EnvironmentVariables = new Dictionary<string, string>();
        
        // Provide connection string
        var connectionString = Fn.Join("", [
            "Host=",
            dbInstance.DbInstanceEndpointAddress,
            ";Port=",
            dbInstance.DbInstanceEndpointPort,
            ";Database=",
            linkedAnnotation.Resource.Name,
            ";Username={username};Password={password}"
        ]);
        
        result.EnvironmentVariables[$"ConnectionStrings__{linkedAnnotation.Resource.Name}"] = 
            connectionString;

        return result;
    }

    public override bool ReferenceRequiresVPC()
    {
        return true;  // RDS instances are in VPC
    }

    public override bool ReferenceRequiresSecurityGroup()
    {
        return true;  // Need security group for database access
    }

    public override void ApplyReferenceSecurityGroup(
        AWSLinkedObjectsAnnotation linkedAnnotation, 
        ISecurityGroup securityGroup)
    {
        if (linkedAnnotation.Construct is not DatabaseInstance dbInstance)
            return;

        // Add ingress rule to allow access from the referencing resource
        dbInstance.Connections.AllowFrom(
            securityGroup, 
            Port.Tcp(5432), 
            "Allow PostgreSQL access from referencing resource"
        );
    }
}
```

#### Step 3: Add to CDKDefaultsProvider

In the `CDKDefaultsProvider.PublishTargets.cs` file the new enum for the PostgresResource will be created along with the virtual property
identifing the default. The docs for the enum should contain a link to the CDK construct documentation.

```csharp
public partial class CDKDefaultsProvider
{
        /// <summary>
        /// Deploy to RDS as a Postgress database.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_docdb.CfnDBInstance.html">CfnDBInstance</a> construct is used to create the ECS Express Gateway service.
        /// </summary>
    public enum PostgresResourcePublishTarget
    {
        RDSPostgres // (Potential over publishing targets are Aurora and DSQL)
    }

    public virtual PostgresResourcePublishTarget DefaultPostgresResourcePublishTarget 
        { get; set; } = PostgresResourcePublishTarget.RDSPostgres;

   ... Other publishing target enums and default properties
}
```


For the default values for the CDK construct props object and the Apply method should be defined in a partial class based on the publish target name. 
In this case it would be called `CDKDefaultsProvider.RDSPostgres.cs`. This allows easy navigation to the properties that are specific to the publish target.
All default value should be defined as a virtual .NET property allowing subclasses to change the values. Since all default values are defined in the same
class each property should be prefixed by the publish target name. In this case each property should start with `RDSPostgres`. The comments on the property
must add a `remarks` section that says what the default value is.

The Apply method should be `protected internal` for the publishing target to call and to be customized via subclasses. These methods should be be called directly
by end users and so should not be `public`.

```csharp
public partial class CDKDefaultsProvider
{

    /// <summary>
    /// Gets the major allocated storage used for RDS database.
    /// </summary>
    /// <remarks>
    /// Default is 20.
    /// </remarks>
    public virtual string RDSPostgresAllocatedStorage => 20";

    protected internal virtual void ApplyRDSPostgresDefaults(
        DatabaseInstanceProps props, 
        PostgresResource resource)
    {
        props.AllocatedStorage ??= RDSPostgresAllocatedStorage;

        ...
    }
}
```


#### Step 4: Register the Publish Target

Add to the service registration in `AWSCDKEnvironmentExtensions.cs`:

```csharp
private static void AddEnvironmentServices(this IDistributedApplicationBuilder builder)
{
    // ... existing registrations ...
    builder.Services.AddTransient<IAWSPublishTarget, RDSPostgresPublishTarget>();
}
```

#### Step 5: Create Extension Method

```csharp
public static class PostgresPublishExtensions
{
    public static IResourceBuilder<PostgresResource> PublishAsRDSPostgres(
        this IResourceBuilder<PostgresResource> builder, 
        PublishRDSPostgresConfig? config = null)
    {
        var annotation = new PublishRDSPostgresAnnotation 
        { 
            Config = config ?? new PublishRDSPostgresConfig() 
        };
        builder.Resource.Annotations.Add(annotation);
        return builder;
    }
}
```

#### Step 6: Usage

```csharp
var postgres = builder.AddPostgres("mydb")
    .PublishAsRDSPostgres(new PublishRDSPostgresConfig
    {
        PropsDatabaseInstanceCallback = (ctx, props) =>
        {
            props.InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.SMALL);
            props.AllocatedStorage = 100;
        }
    });

var webApp = builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(postgres);  // Will get connection string via environment variable
```

---

### Scenario 2: Alternative Publish Target

When adding an alternative publishing target for a resource follow all of the same steps as Scenario 1 except instead of adding a 
new publishing enum like the `PostgresResourcePublishTarget` and a new enum value to the existing enum. Also the the default property
like `DefaultPostgresResourcePublishTarget` must be left alone. 

If the alternative publish target is meant to be the new default then as discussed with CDKDefaultsProvider create a new version
CDKDefaultsProvider and then override the defaults property using the new default value.   

---

## Summary

The AWS deployment system for .NET Aspire provides a flexible, extensible architecture for transforming Aspire resources into AWS infrastructure:

1. **Environment Setup**: Use `AddAWSCDKEnvironment()` to configure deployment with versioned defaults
2. **Custom Stacks**: Provide your own CDK Stack to define custom infrastructure
3. **Publish Overrides**: Use Publish extension methods to override default deployment targets
4. **Customization**: Use callbacks to modify CDK props and constructs
5. **Default Constructs**: Override shared resources using attributes in your custom stack
6. **Versioned Defaults**: CDKDefaultsProvider allows opt-in to breaking changes
7. **Automatic Selection**: Resources without explicit publish targets are matched automatically
8. **Reference Handling**: Automatic configuration of environment variables, VPC, and security groups
9. **Extensibility**: Add new publish targets for new resource types or alternative deployment options

This design enables both simple deployments with sensible defaults and complex customizations for production workloads.
