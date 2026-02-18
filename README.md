## Integrations with .NET Aspire for AWS

This repositry contains the integrations with [.NET Aspire](https://github.com/dotnet/aspire) for AWS. The AWS integrations focus on provisioning and working with AWS application resources in development environment. Making the dev inner loop of iterating over application code with AWS resource seamless without having to leave the development environment.

For introduction on using AWS and Aspire checkout the [Building .NET Applications Across Clouds with .NET Aspire](https://www.youtube.com/watch?v=yVgr6cRYOPk) talk as part of .NET Conf 2024.

[![image](./resources/dotnetconf-2024-session.jpg)](https://www.youtube.com/watch?v=yVgr6cRYOPk)

## Integrations

The following are the list of AWS integrations currently supported for .NET Aspire.

### Aspire.Hosting.AWS

The hosting package to include in Aspire AppHost projects for provisioning and configuring AWS resources for Aspire applications. The package contains the following features. 

* Configure AWS credentials and region for AWS SDK for .NET
* Provisioning AWS resources with CloudFormation template
* Provisioning AWS resources with AWS Cloud Development Kit (CDK)
* Local development with DynamoDB
* Local development with AWS Lambda and API Gateway

Check out the package's [README](./src/Aspire.Hosting.AWS/README.md) for a deeper explanation of these features.

[![nuget](https://img.shields.io/nuget/v/Aspire.Hosting.AWS.svg) ![downloads](https://img.shields.io/nuget/dt/Aspire.Hosting.AWS.svg)](https://www.nuget.org/packages/Aspire.Hosting.AWS/)

## Deployment to AWS

> **Note**: This feature is currently in preview and subject to change based on feedback. We encourage you to try it out and share your feedback!

The AWS deployment feature for .NET Aspire enables you to deploy your Aspire applications directly to AWS. The deployment system transforms your Aspire AppHost resources into AWS CDK constructs, which are then synthesized into CloudFormation templates and deployed to your AWS account. This provides a seamless path from local development to cloud deployment.

For comprehensive documentation including advanced scenarios, implementation details, and architectural guidance, see the [Deployment Design Document](./docs/deployment-design.md).

### Prerequisites

Before deploying to AWS, ensure you have:

* **Node.js 22.x** - Required for AWS CDK, which the deployment system uses to generate CloudFormation templates
* **AWS CDK** installed globally: `npm install -g aws-cdk`
* **AWS CDK Bootstrap** - Must be run on your target AWS account and region before first deployment: `cdk bootstrap aws://ACCOUNT-NUMBER/REGION`

The deployment system leverages AWS CDK to transform your Aspire resources into cloud infrastructure, which requires Node.js and the CDK CLI to be available in your environment.

### Getting Started

Add an AWS CDK environment to your AppHost to enable deployment:

```csharp
// Add to opt-in to using the preview publish/deployment APIs.
#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

// Add AWS CDK environment
builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1
);

// Add your resources
var webApp = builder.AddProject<Projects.WebApp>("webapp");

builder.Build().Run();
```

The `name` parameter identifies your application stack in AWS, and the `cdkDefaultsProviderFactory` parameter specifies which version of default behaviors to use (currently `Preview_V1` for the preview release).

**Deploying Your Application:**

To deploy your application to AWS, use the Aspire CLI:

* **`aspire publish`** - Transforms your Aspire resources into AWS CDK constructs and synthesizes them into a `cdk.out` directory containing CloudFormation templates
* **`aspire deploy`** - Runs the publish step and then uses the AWS CDK CLI to deploy the `cdk.out` directory to your AWS account

The deployment process will prompt you for AWS credentials and region if not already configured.

### Basic Workflows

#### Automatic Resource Mapping

By default, Aspire resources are automatically mapped to appropriate AWS services based on their type:

```csharp
// Add to opt-in to using the preview publish/deployment APIs.
#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1
);

// Web projects automatically deploy to ECS Fargate Express
var webApp = builder.AddProject<Projects.WebApp>("webapp");

// Lambda functions automatically deploy to AWS Lambda
var function = builder.AddAWSLambdaFunction<Projects.MyFunction>("function", "<lambda-function-handler>");

// Redis automatically deploys to ElastiCache
var cache = builder.AddRedis("cache");

builder.Build().Run();
```

The deployment system analyzes each resource and selects the most appropriate AWS service automatically. You don't need to specify deployment targets unless you want to customize the defaults.

#### Customizing Deployment Targets

You can override the default deployment target for any resource using Publish extension methods:

```csharp
// Add to opt-in to using the preview publish/deployment APIs.
#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1
);

// Deploy web app with Application Load Balancer instead of default Express service
var webApp = builder.AddProject<Projects.WebApp>("webapp")
    .PublishAsECSFargateServiceWithALB();

builder.Build().Run();
```

Each Publish method allows you to customize the AWS service configuration through callbacks that modify CDK construct properties. See the [Deployment Design Document](./docs/deployment-design.md) for details on available Publish methods and customization options.

#### Connecting Resources

Use `WithReference()` to connect resources - the deployment system automatically configures all necessary connectivity:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAWSCDKEnvironment(
    name: "MyApp",
    cdkDefaultsProviderFactory: CDKDefaultsProviderFactory.Preview_V1
);

var cache = builder.AddRedis("cache");

// Connect the web app to the cache
var webApp = builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(cache);

builder.Build().Run();
```

When resources are connected with `WithReference()`, the system automatically:
* Configures environment variables with connection strings and endpoints
* Attaches resources to the same VPC when required (e.g., for ElastiCache)
* Sets up security groups to allow network access between resources

Your application code can access the referenced resource using the standard .NET Aspire configuration patterns - no AWS-specific code required.

### Advanced Scenarios

For advanced deployment scenarios and comprehensive implementation details, see the [Deployment Design Document](./docs/deployment-design.md). The design document covers:

- **[Custom CDK Stacks](./docs/deployment-design.md#custom-cdk-stacks)**: Define your own infrastructure alongside Aspire resources, override default constructs (VPC, ECS Cluster, etc.), and integrate with existing AWS infrastructure
- **[CDK Props and Callbacks](./docs/deployment-design.md#cdk-props-and-construct-callbacks)**: Customize CDK construct properties and behavior through configuration callbacks
- **[CDKDefaultsProvider System](./docs/deployment-design.md#cdkdefaultsprovider-system)**: Control default values and behaviors with versioned providers, allowing you to opt-in to breaking changes when ready
- **[Adding New Publish Targets](./docs/deployment-design.md#adding-new-publish-targets)**: Extend the deployment system to support additional AWS services or alternative deployment options

The design document provides detailed examples and implementation guidance for these advanced use cases, enabling you to customize deployments for production workloads.

## Getting Help

For feature requests or issues using this tool please open an [issue in this repository](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues).

## Contributing
We welcome community contributions and pull requests. See [CONTRIBUTING](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/blob/main/CONTRIBUTING.md) for information on how to set up a development environment and submit code.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.

