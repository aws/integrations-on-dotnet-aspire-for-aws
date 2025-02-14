// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;
using Amazon.CDK.AWS.S3;
using Aspire.Hosting.AWS.UnitTests.Utils;
using Constructs;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class AWSCDKResourceTests
{
    [Fact]
    public void AddAWSCDKResourceTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.EUWest1)
            .WithProfile("test-profile");

        var resource = builder.AddAWSCDKStack("Stack")
            .WithReference(awsSdkConfig)
            .Resource;

        Assert.Equal("Stack", resource.Name);
        Assert.NotNull(resource.AWSSDKConfig);
        Assert.Equal(RegionEndpoint.EUWest1, resource.AWSSDKConfig.Region);
        Assert.Equal("test-profile", resource.AWSSDKConfig.Profile);
    }

    [Fact]
    public void AddAWSCDKResourceWithAdditionalStackTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.EUWest1)
            .WithProfile("test-profile");

        var cdk = builder
            .AddAWSCDKStack("Stack")
            .WithReference(awsSdkConfig);
        var resource = builder
            .AddAWSCDKStack("Other")
            .WithReference(awsSdkConfig).Resource;

        Assert.Equal("Other", resource.Name);
        Assert.NotNull(resource.AWSSDKConfig);
        Assert.Equal(RegionEndpoint.EUWest1, resource.AWSSDKConfig.Region);
        Assert.Equal("test-profile", resource.AWSSDKConfig.Profile);
    }

    [Fact]
    public void AddAWSCDKResourceWithAdditionalStackAndConfigTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.EUWest1)
            .WithProfile("test-profile");
        var awsSdkStackConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.EUWest2)
            .WithProfile("other-test-profile");

        var cdk = builder.AddAWSCDKStack("Stack")
            .WithReference(awsSdkConfig);
        var cdkResource = cdk.Resource;
        var stackResource = builder.AddAWSCDKStack("Other").WithReference(awsSdkStackConfig).Resource;

        // Assert Stack resource
        Assert.Equal("Other", stackResource.Name);
        Assert.NotNull(stackResource.AWSSDKConfig);
        Assert.Equal(RegionEndpoint.EUWest2, stackResource.AWSSDKConfig.Region);
        Assert.Equal("other-test-profile", stackResource.AWSSDKConfig.Profile);

        // Assert CDK resource
        Assert.Equal("Stack", cdkResource.Name);
        Assert.NotNull(cdkResource.AWSSDKConfig);
        Assert.Equal(RegionEndpoint.EUWest1, cdkResource.AWSSDKConfig.Region);
        Assert.Equal("test-profile", cdkResource.AWSSDKConfig.Profile);
    }

    [Fact]
    public void AddAWSCDKResourceWithConstructTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cdk = builder.AddAWSCDKStack("Stack");
        var resource = cdk.AddConstruct("Construct", scope => new Construct(scope, "Construct")).Resource;

        Assert.Equal("Construct", resource.Name);
        Assert.Equal(cdk.Resource, resource.Parent);
    }

    [Fact]
    public async Task ManifestAWSCDKResourceTest()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cdk = builder.AddAWSCDKStack("Stack");
        var resourceBuilder = cdk.AddConstruct("Construct", scope => new Bucket(scope, "Bucket"));

        builder.AddProject<ServiceA>("ServiceA", o => o.ExcludeLaunchProfile = true)
            .WithReference(resourceBuilder, bucket => bucket.BucketName, "BucketName");

        var resource = cdk.Resource;
        Assert.NotNull(resource);

        const string expectedManifest = """
                                        {
                                          "type": "aws.cloudformation.template.v0",
                                          "stack-name": "Stack",
                                          "template-path": "cdk.out/Stack.template.json",
                                          "references": [
                                            {
                                              "target-resource": "ServiceA"
                                            }
                                          ]
                                        }
                                        """;

        var manifest = await ManifestUtils.GetManifest(resource);
        Assert.Equal(expectedManifest, manifest.ToString());
    }

    private sealed class ServiceA : IProjectMetadata
    {
        public string ProjectPath => "ServiceA";
    }
}
