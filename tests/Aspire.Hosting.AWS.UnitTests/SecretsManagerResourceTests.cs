// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;
using Amazon.CDK.AWS.SecretsManager;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.UnitTests.Utils;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class SecretsManagerResourceTests
{
    [Fact]
    public void AddSecretTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var secret = stack.AddSecret("MySecret");

        Assert.NotNull(secret);
        Assert.Equal("MySecret", secret.Resource.Name);
        Assert.IsAssignableFrom<IConstructResource<Secret>>(secret.Resource);
    }

    [Fact]
    public void AddSecretWithPropsTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var props = new SecretProps
        {
            Description = "Test secret for unit testing"
        };

        var secret = stack.AddSecret("MySecret", props);

        Assert.NotNull(secret);
        Assert.Equal("MySecret", secret.Resource.Name);
    }

    [Fact]
    public void AddGeneratedSecretTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var generator = new SecretStringGenerator
        {
            SecretStringTemplate = "{\"username\":\"admin\"}",
            GenerateStringKey = "password",
            PasswordLength = 32
        };

        var secret = stack.AddGeneratedSecret("DatabaseCredentials", generator, "Auto-generated database credentials");

        Assert.NotNull(secret);
        Assert.Equal("DatabaseCredentials", secret.Resource.Name);
    }

    [Fact]
    public void AddSecretWithReferenceTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var secret = stack.AddSecret("AppSecret");

        var project = builder.AddProject<AWSTestProject>("TestProject")
            .WithReference(secret);

        Assert.NotNull(project);

        // Verify environment variables are set
        var envAnnotations = project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void AddSecretWithCustomConfigSectionTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var secret = stack.AddSecret("AppSecret");

        var project = builder.AddProject<AWSTestProject>("TestProject")
            .WithReference(secret, "CustomSecrets:App");

        Assert.NotNull(project);

        var envAnnotations = project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void AddSecretWithDirectEnvVarReferenceTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var secret = stack.AddSecret("ApiKey");

        var project = builder.AddProject<AWSTestProject>("TestProject")
            .WithSecretReference(secret, "API_KEY_ARN");

        Assert.NotNull(project);

        var envAnnotations = project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void AddMultipleSecretsTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var dbSecret = stack.AddSecret("DatabasePassword");
        var apiSecret = stack.AddSecret("ApiKey");
        var appSecret = stack.AddGeneratedSecret("AppSecret", new SecretStringGenerator
        {
            PasswordLength = 64
        });

        var project = builder.AddProject<AWSTestProject>("TestProject")
            .WithReference(dbSecret, "Database:Secret")
            .WithReference(apiSecret, "Api:Secret")
            .WithSecretReference(appSecret, "APP_SECRET_ARN");

        Assert.NotNull(project);

        var envAnnotations = project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void SecretManifestOutputTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2);

        var stack = builder.AddAWSCDKStack("TestStack")
            .WithReference(awsSdkConfig);

        var secret = stack.AddSecret("TestSecret");

        // Verify the resource can be added to manifest
        var manifest = ManifestUtils.GetManifest(secret.Resource);
        Assert.NotNull(manifest);
        Assert.Equal("aws.cdk.construct.v0", manifest["type"]?.ToString());
    }
}
