// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001

using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
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
        Assert.IsType<PublishS3WithCloudFrontAnnotation>(result.PublishTargetAnnotation);
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

    // --- GenerateConstructAsync: always CloudFront ---

    [Fact]
    public async Task GenerateConstructAsync_CreatesPrivateBucketWithCloudFront()
    {
        BucketProps? capturedProps = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.PropsBucketCallback = (_, props) => capturedProps = props;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedProps);
        // Defaults applied after callback — verify defaults provider filled them in
        Assert.Equal(BlockPublicAccess.BLOCK_ALL, capturedProps.BlockPublicAccess);
        Assert.True(capturedProps.EnforceSSL);
    }

    [Fact]
    public async Task GenerateConstructAsync_CreatesDistribution()
    {
        var (environment, resource, annotation) = SetupTest();

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        var allConstructs = environment.CDKStack.Node.FindAll();
        Assert.Contains(allConstructs, c => c is Distribution);
    }

    [Fact]
    public async Task GenerateConstructAsync_EmitsCloudFrontUrlOutput()
    {
        var (environment, resource, annotation) = SetupTest();

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(environment.CDKStack.Node.TryFindChild($"{resource.Name}-CloudFrontUrl"));
        Assert.Null(environment.CDKStack.Node.TryFindChild($"{resource.Name}-S3WebsiteUrl"));
    }

    [Fact]
    public async Task GenerateConstructAsync_DistributionHasSpaErrorResponses()
    {
        DistributionProps? capturedProps = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            config.PropsDistributionCallback = (_, props) => capturedProps = props;
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        // Error responses are applied after the callback by the defaults provider on the same props object
        Assert.NotNull(capturedProps?.ErrorResponses);
        var statuses = capturedProps!.ErrorResponses!.Select(e => (int)e.HttpStatus).ToHashSet();
        Assert.Contains(403, statuses);
        Assert.Contains(404, statuses);
        Assert.All(capturedProps.ErrorResponses!, e =>
        {
            Assert.Equal(200, (int)e.ResponseHttpStatus!);
            Assert.Equal("/index.html", e.ResponsePagePath);
        });
    }

    [Fact]
    public async Task GenerateConstructAsync_BucketDefaultsAppliedAfterCallback()
    {
        BucketProps? capturedAfterDefault = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
            // callback runs first; defaults fill in what's missing afterward
            config.PropsBucketCallback = (_, props) =>
            {
                // deliberately leave BlockPublicAccess null so defaults provider sets it
                capturedAfterDefault = props;
            };
        });

        await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        Assert.NotNull(capturedAfterDefault);
        // After GenerateConstructAsync defaults were applied on the same props object
        Assert.Equal(BlockPublicAccess.BLOCK_ALL, capturedAfterDefault.BlockPublicAccess);
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
    public async Task GenerateConstructAsync_InvokesDistributionCallbacks()
    {
        var propsCallbackInvoked = false;
        Distribution? capturedDistribution = null;
        var (environment, resource, annotation) = SetupTest(config =>
        {
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

    // --- GetReferenceConnectionInfo ---

    [Fact]
    public async Task GetReferenceConnectionInfo_ReturnsCloudFrontHttpsEndpoint()
    {
        var (environment, resource, annotation) = SetupTest();
        var target = CreateTarget();

        await target.GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

        var linkedAnnotation = resource.Annotations.OfType<AWSLinkedObjectsAnnotation>().LastOrDefault();
        Assert.NotNull(linkedAnnotation);
        Assert.IsType<Distribution>(linkedAnnotation!.Construct);

        var info = target.GetReferenceConnectionInfo(linkedAnnotation!);

        Assert.NotNull(info.EnvironmentVariables);
        Assert.True(info.EnvironmentVariables!.ContainsKey($"services__{resource.Name}__https__0"));
    }

    // --- Backend behaviors (via CloudFrontBehaviorAnnotation) ---

    [Fact]
    public async Task GenerateConstructAsync_BackendBehavior_MissingAnnotation_ThrowsWithClearMessage()
    {
        var backendResource = new Aspire.Hosting.ApplicationModel.ProjectResource("backend");
        var (environment, resource, annotation) = SetupTest();
        resource.Annotations.Add(new CloudFrontBehaviorAnnotation("/api/*", backendResource));

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

        // An S3 Bucket exposes no service-discovery URL, so GetReferenceConnectionInfo returns nothing
        var unsupportedConstruct = new Amazon.CDK.AWS.S3.Bucket(stack, "UnsupportedBackend");
        var backendResource = new Aspire.Hosting.ApplicationModel.ProjectResource("backend");
        var dummyTarget = CreateTarget();
        backendResource.Annotations.Add(new AWSLinkedObjectsAnnotation
        {
            EnvironmentResource = environment,
            Resource = backendResource,
            Construct = unsupportedConstruct,
            PublishTarget = dummyTarget,
        });

        var resource = new JavaScriptAppResource("frontend", "npm", _workingDirectory);
        var annotation = new PublishS3WithCloudFrontAnnotation { WorkingDirectory = _workingDirectory };
        resource.Annotations.Add(new CloudFrontBehaviorAnnotation("/api/*", backendResource));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None));

        Assert.Contains("backend", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }

    // --- Custom output path ---

    [Fact]
    public async Task GenerateConstructAsync_UsesCustomOutputPath()
    {
        // Use an isolated working directory that only contains "build/" — no "dist/".
        // If the code ignores OutputPath and falls back to the default "dist", CDK's
        // Source.Asset will throw because "dist/" doesn't exist, proving the path is used.
        var isolatedDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var customOutputDir = Path.Combine(isolatedDir, "build");
        Directory.CreateDirectory(customOutputDir);
        File.WriteAllText(Path.Combine(customOutputDir, "index.html"), "<html/>");

        try
        {
            BucketDeploymentProps? capturedDeploymentProps = null;
            var (environment, resource, annotation) = SetupTest(isolatedDir, config =>
            {
                config.OutputPath = "build";
                config.PropsBucketDeploymentCallback = (_, props) => capturedDeploymentProps = props;
            });

            await CreateTarget().GenerateConstructAsync(environment, resource, annotation, CancellationToken.None);

            Assert.NotNull(capturedDeploymentProps);
            Assert.Single(capturedDeploymentProps!.Sources);
        }
        finally
        {
            Directory.Delete(isolatedDir, recursive: true);
        }
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

    private (AWSCDKEnvironmentResource<Stack> Environment, JavaScriptAppResource Resource, PublishS3WithCloudFrontAnnotation Annotation) SetupTest(
        Action<PublishS3WithCloudFrontConfig>? configure = null)
        => SetupTest(_workingDirectory, configure);

    private (AWSCDKEnvironmentResource<Stack> Environment, JavaScriptAppResource Resource, PublishS3WithCloudFrontAnnotation Annotation) SetupTest(
        string workingDirectory,
        Action<PublishS3WithCloudFrontConfig>? configure = null)
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

        var resource = new JavaScriptAppResource("frontend", "npm", workingDirectory);
        var annotation = new PublishS3WithCloudFrontAnnotation { WorkingDirectory = workingDirectory };
        configure?.Invoke(annotation.Config);

        return (environment, resource, annotation);
    }
}
