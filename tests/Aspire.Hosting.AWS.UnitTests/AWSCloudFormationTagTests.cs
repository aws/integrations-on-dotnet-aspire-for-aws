// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.AWS.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests;

public class AWSCloudFormationTagTests
{
    [Fact]
    public void WithTag_CloudFormationStackResource_AddsTagToResource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        // Act
        var resource = builder.AddAWSCloudFormationStack("ExistingStack")
            .WithReference(awsSdkConfig)
            .WithTag("Environment", "Test")
            .WithTag("Project", "UnitTest")
            .Resource;

        // Assert
        Assert.Equal(2, resource.Tags.Count);
        Assert.Equal("Test", resource.Tags["Environment"]);
        Assert.Equal("UnitTest", resource.Tags["Project"]);
    }

    [Fact]
    public void WithTag_CloudFormationTemplateResource_AddsTagToResource()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var awsSdkConfig = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USWest2)
            .WithProfile("test-profile");

        // Act
        var resource = builder.AddAWSCloudFormationTemplate("NewStack", "cf.template")
            .WithReference(awsSdkConfig)
            .WithParameter("key1", "value1")
            .WithTag("Environment", "Test")
            .WithTag("Project", "UnitTest")
            .Resource;

        // Assert
        Assert.Equal(2, resource.Tags.Count);
        Assert.Equal("Test", resource.Tags["Environment"]);
        Assert.Equal("UnitTest", resource.Tags["Project"]);
    }

    [Fact]
    public void CloudFormationExecutionContext_IncludesTags()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var templateResource = builder.AddAWSCloudFormationTemplate("TaggedStack", "cf.template")
            .WithTag("Environment", "Test")
            .WithTag("Project", "UnitTest")
            .Resource as CloudFormationTemplateResource;

        Assert.NotNull(templateResource);
        
        // Create an execution context
        var context = new CloudFormationStackExecutionContext(templateResource.StackName, "{}");
        
        // Copy tags from resource to context
        context.Tags = templateResource.Tags;
        
        // Assert
        Assert.Equal(2, context.Tags.Count);
        Assert.Equal("Test", context.Tags["Environment"]);
        Assert.Equal("UnitTest", context.Tags["Project"]);
    }
}
