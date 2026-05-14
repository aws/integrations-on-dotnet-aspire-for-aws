// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001

using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using AWSLinkedObjectsAnnotation = Aspire.Hosting.AWS.Deployment.CDKPublishTargets.AWSLinkedObjectsAnnotation;
using Aspire.Hosting.AWS.Deployment.Services;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class S3StaticWebsitePublishTargetTests : IDisposable
{
    private readonly string _workingDirectory;

    public S3StaticWebsitePublishTargetTests()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // CDK's BucketDeployment validates Source.Asset paths at construct creation time
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "dist"));
        File.WriteAllText(Path.Combine(_workingDirectory, "dist", "index.html"), "<html/>");
    }

    public void Dispose() => Directory.Delete(_workingDirectory, recursive: true);

    // --- IsDefaultPublishTargetMatch ---

    [Fact]
    public void IsDefaultPublishTargetMatch_JavaScriptAppResource_ReturnsMatch()
    {
        var target = CreateTarget();
        var resource = new JavaScriptAppResource("frontend", "npm", _workingDirectory);

        var result = target.IsDefaultPublishTargetMatch(null!, resource);

        Assert.True(result.IsMatch);
        Assert.IsType<PublishS3StaticWebsiteAnnotation>(result.PublishTargetAnnotation);
        Assert.Equal(IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK, result.Rank);
    }

    [Fact]
    public void IsDefaultPublishTargetMatch_NonJavaScriptResource_ReturnsNoMatch()
    {
        var target = CreateTarget();
        var resource = new Aspire.Hosting.ApplicationModel.ProjectResource("backend");

        var result = target.IsDefaultPublishTargetMatch(null!, resource);

        Assert.False(result.IsMatch);
    }

    // --- GenerateConstructAsync: S3-only mode ---

    [Fact]
    public async Task GenerateConstructAsync_S3Only_CreatesBucketWithWebsiteHosting()
    {
        BucketProps? capturedProps = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.PropsBucketCallback = (_, props) => capturedProps = props;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedProps);
        Assert.Equal("index.html", capturedProps.WebsiteIndexDocument);
        Assert.Equal("index.html", capturedProps.WebsiteErrorDocument);
        Assert.True(capturedProps.PublicReadAccess as bool? ?? false);
        Assert.Equal(BlockPublicAccess.BLOCK_ACLS_ONLY, capturedProps.BlockPublicAccess);
    }

    [Fact]
    public async Task GenerateConstructAsync_S3Only_EmitsS3WebsiteUrlOutput()
    {
        var (environment, resource, annotation) = SetupTest();

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(environment.CDKStack.Node.TryFindChild($"{resource.Name}-S3WebsiteUrl"));
        Assert.Null(environment.CDKStack.Node.TryFindChild($"{resource.Name}-CloudFrontUrl"));
    }

    [Fact]
    public async Task GenerateConstructAsync_S3Only_DoesNotCreateDistribution()
    {
        var (environment, resource, annotation) = SetupTest();

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        var allConstructs = environment.CDKStack.Node.FindAll();
        Assert.DoesNotContain(allConstructs, c => c is Distribution);
    }

    // --- GenerateConstructAsync: CloudFront mode ---

    [Fact]
    public async Task GenerateConstructAsync_WithCloudFront_CreatesPrivateBucket()
    {
        BucketProps? capturedProps = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.WithCloudFront = true;
            config.PropsBucketCallback = (_, props) => capturedProps = props;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedProps);
        Assert.Equal(BlockPublicAccess.BLOCK_ALL, capturedProps.BlockPublicAccess);
        Assert.Null(capturedProps.WebsiteIndexDocument);
    }

    [Fact]
    public async Task GenerateConstructAsync_WithCloudFront_CreatesDistribution()
    {
        var (environment, resource, annotation) = SetupTest(config => config.WithCloudFront = true);

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        var allConstructs = environment.CDKStack.Node.FindAll();
        Assert.Contains(allConstructs, c => c is Distribution);
    }

    [Fact]
    public async Task GenerateConstructAsync_WithCloudFront_EmitsCloudFrontUrlOutput()
    {
        var (environment, resource, annotation) = SetupTest(config => config.WithCloudFront = true);

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(environment.CDKStack.Node.TryFindChild($"{resource.Name}-CloudFrontUrl"));
        Assert.Null(environment.CDKStack.Node.TryFindChild($"{resource.Name}-S3WebsiteUrl"));
    }

    [Fact]
    public async Task GenerateConstructAsync_WithCloudFront_DistributionHasSpaErrorResponses()
    {
        DistributionProps? capturedProps = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.WithCloudFront = true;
            config.PropsDistributionCallback = (_, props) => capturedProps = props;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedProps?.ErrorResponses);
        var statuses = capturedProps!.ErrorResponses!.Cast<IErrorResponse>().Select(e => e.HttpStatus).ToList();
        Assert.Contains(403, statuses);
        Assert.Contains(404, statuses);
    }

    // --- Callback invocation ---

    [Fact]
    public async Task GenerateConstructAsync_InvokesPropsBucketCallback()
    {
        var callbackInvoked = false;
        var (environment, resource, annotation) = SetupTest(config =>
            config.PropsBucketCallback = (_, _) => callbackInvoked = true);

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task GenerateConstructAsync_InvokesConstructBucketCallback()
    {
        Bucket? capturedBucket = null;
        var (environment, resource, annotation) = SetupTest(config =>
            config.ConstructBucketCallback = (_, bucket) => capturedBucket = bucket);

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedBucket);
    }

    [Fact]
    public async Task GenerateConstructAsync_WithCloudFront_InvokesDistributionCallbacks()
    {
        var propsCallbackInvoked = false;
        Distribution? capturedDistribution = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.WithCloudFront = true;
            config.PropsDistributionCallback = (_, _) => propsCallbackInvoked = true;
            config.ConstructDistributionCallback = (_, d) => capturedDistribution = d;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.True(propsCallbackInvoked);
        Assert.NotNull(capturedDistribution);
    }

    [Fact]
    public async Task GenerateConstructAsync_InvokesSiteBuilderWithReferenceEnvVars()
    {
        var mockBuilder = new Mock<IStaticSiteBuilder>();
        var (environment, resource, annotation) = SetupTest();

        await CreateTarget(mockBuilder).GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        mockBuilder.Verify(b => b.BuildAsync(
            resource,
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Backend behaviors ---

    [Fact]
    public async Task GenerateConstructAsync_BackendBehavior_MissingAnnotation_ThrowsWithClearMessage()
    {
        var backendResource = new Aspire.Hosting.ApplicationModel.ProjectResource("backend");
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.WithCloudFront = true;
            config.AddBackendBehavior("/api/*", backendResource);
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None));

        Assert.Contains("backend", ex.Message);
        Assert.Contains("not been published yet", ex.Message);
    }

    [Fact]
    public async Task GenerateConstructAsync_BackendBehavior_UnsupportedConstructType_ThrowsWithClearMessage()
    {
        var app = new App();
        var stack = new Stack(app, $"TestStack-{Guid.NewGuid():N}");
        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (_, _) => stack,
            null);
        environment.InitializeCDKApp(null, Path.GetTempPath());

        // An S3 Bucket is an unsupported construct type for a backend behavior
        var unsupportedConstruct = new Amazon.CDK.AWS.S3.Bucket(stack, "UnsupportedBackend");
        var backendResource = new Aspire.Hosting.ApplicationModel.ProjectResource("backend");
        backendResource.Annotations.Add(new AWSLinkedObjectsAnnotation
        {
            EnvironmentResource = environment,
            Resource = backendResource,
            Construct = unsupportedConstruct,
            PublishTarget = CreateTarget(),
        });

        var resource = new JavaScriptAppResource("frontend", "npm", _workingDirectory);
        var annotation = new PublishS3StaticWebsiteAnnotation { WorkingDirectory = _workingDirectory };
        annotation.Config.WithCloudFront = true;
        annotation.Config.AddBackendBehavior("/api/*", backendResource);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None));

        Assert.Contains("backend", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }

    // --- Custom output path ---

    [Fact]
    public async Task GenerateConstructAsync_UsesCustomOutputPath()
    {
        var customOutputDir = Path.Combine(_workingDirectory, "build");
        Directory.CreateDirectory(customOutputDir);
        File.WriteAllText(Path.Combine(customOutputDir, "index.html"), "<html/>");

        var (environment, resource, annotation) = SetupTest(config => config.OutputPath = "build");

        // Should not throw — means the custom path was used for Source.Asset
        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);
    }

    // --- Helpers ---

    private S3StaticWebsitePublishTarget CreateTarget(Mock<IStaticSiteBuilder>? mockBuilder = null)
    {
        mockBuilder ??= new Mock<IStaticSiteBuilder>();
        mockBuilder.Setup(b => b.BuildAsync(
            It.IsAny<Aspire.Hosting.ApplicationModel.IResource>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new S3StaticWebsitePublishTarget(mockBuilder.Object, NullLogger<S3StaticWebsitePublishTarget>.Instance);
    }

    private (AWSCDKEnvironmentResource<Stack> Environment, JavaScriptAppResource Resource, PublishS3StaticWebsiteAnnotation Annotation) SetupTest(
        Action<PublishS3StaticWebsiteConfig>? configure = null)
    {
        var app = new App();
        var stack = new Stack(app, $"TestStack-{Guid.NewGuid():N}");
        var environment = new AWSCDKEnvironmentResource<Stack>(
            "test-env",
            isPublishMode: true,
            CDKDefaultsProviderFactory.Preview_V1,
            (_, _) => stack,
            null);
        environment.InitializeCDKApp(null, Path.GetTempPath());

        var resource = new JavaScriptAppResource("frontend", "npm", _workingDirectory);

        var annotation = new PublishS3StaticWebsiteAnnotation { WorkingDirectory = _workingDirectory };
        configure?.Invoke(annotation.Config);

        return (environment, resource, annotation);
    }
}
