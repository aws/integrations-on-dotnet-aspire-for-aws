// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIRECOMPUTE001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class ProcessRelationShipsTests
{
    /// <summary>
    /// Tests that static string WithEnvironment values are resolved and added to env vars.
    /// Covers issue #169 item 3: Inline static WithEnvironment values.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_StaticString_AddsToEnvironmentVariables()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");

        // Simulate WithEnvironment("MY_KEY", "my-value") — Aspire registers an EnvironmentCallbackAnnotation
        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["MY_KEY"] = "my-value";
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert
        Assert.NotNull(connectionPoints.EnvironmentVariables);
        Assert.True(connectionPoints.EnvironmentVariables.ContainsKey("MY_KEY"));
        Assert.Equal("my-value", connectionPoints.EnvironmentVariables["MY_KEY"]);
    }

    /// <summary>
    /// Tests that multiple static WithEnvironment calls are all resolved.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_MultipleStaticStrings_AllResolved()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["KEY_A"] = "value-a";
            context.EnvironmentVariables["KEY_B"] = "value-b";
            return Task.CompletedTask;
        }));

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["KEY_C"] = "value-c";
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert
        Assert.Equal("value-a", connectionPoints.EnvironmentVariables!["KEY_A"]);
        Assert.Equal("value-b", connectionPoints.EnvironmentVariables!["KEY_B"]);
        Assert.Equal("value-c", connectionPoints.EnvironmentVariables!["KEY_C"]);
    }

    /// <summary>
    /// Tests that WithEnvironment takes precedence over WithReference-derived env vars.
    /// Covers issue #169 item 5.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_OverridesExistingEnvVars()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");

        // Pre-populate env var (as if set by WithReference)
        connectionPoints.EnvironmentVariables!["EXISTING_KEY"] = "original-value";

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["EXISTING_KEY"] = "overridden-value";
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — WithEnvironment value takes precedence
        Assert.Equal("overridden-value", connectionPoints.EnvironmentVariables!["EXISTING_KEY"]);
    }

    /// <summary>
    /// Tests that IValueProvider env var values (like ParameterResource) are resolved.
    /// Covers issue #169 item 2: Map ParameterResource values.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_IValueProvider_ResolvesValue()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["PARAM_VALUE"] = new TestValueProvider("resolved-param-value");
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert
        Assert.Equal("resolved-param-value", connectionPoints.EnvironmentVariables!["PARAM_VALUE"]);
    }

    /// <summary>
    /// Tests that IValueProvider that resolves to null emits a warning and is skipped.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_IValueProvider_NullValue_SkippedWithWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var (target, connectionPoints) = CreateTestTarget(mockLogger.Object);
        var resource = new TestResource("test-resource");

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["NULL_PARAM"] = new TestValueProvider(null);
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — env var should not be added
        Assert.False(connectionPoints.EnvironmentVariables!.ContainsKey("NULL_PARAM"));
    }

    /// <summary>
    /// Tests that IValueProvider that throws is handled gracefully with a warning.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_IValueProvider_ThrowsException_SkippedWithWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var (target, connectionPoints) = CreateTestTarget(mockLogger.Object);
        var resource = new TestResource("test-resource");

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["FAILING_PARAM"] = new FailingValueProvider();
            return Task.CompletedTask;
        }));

        // Act — should not throw
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — env var should not be added
        Assert.False(connectionPoints.EnvironmentVariables!.ContainsKey("FAILING_PARAM"));
    }

    /// <summary>
    /// Tests that unsupported value types emit a warning and are skipped.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_UnsupportedType_SkippedWithWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var (target, connectionPoints) = CreateTestTarget(mockLogger.Object);
        var resource = new TestResource("test-resource");

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["UNSUPPORTED"] = 42; // int, not string or IValueProvider
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — env var should not be added
        Assert.False(connectionPoints.EnvironmentVariables!.ContainsKey("UNSUPPORTED"));
    }

    /// <summary>
    /// Tests that resources with no EnvironmentCallbackAnnotation work fine (no env vars modified).
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_NoCallbacks_NoModification()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");

        // No annotations added

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — env vars remain empty
        Assert.NotNull(connectionPoints.EnvironmentVariables);
        Assert.Empty(connectionPoints.EnvironmentVariables);
    }

    /// <summary>
    /// Tests that callback context is created with Publish operation mode.
    /// </summary>
    [Fact]
    public async Task ProcessRelationShipsAsync_WithEnvironment_UsesPublishMode()
    {
        // Arrange
        var (target, connectionPoints) = CreateTestTarget();
        var resource = new TestResource("test-resource");
        DistributedApplicationOperation? capturedOperation = null;

        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            capturedOperation = context.ExecutionContext.Operation;
            context.EnvironmentVariables["TEST"] = "test";
            return Task.CompletedTask;
        }));

        // Act
        await target.TestProcessRelationShipsAsync(connectionPoints, resource);

        // Assert — should be in Publish mode
        Assert.NotNull(capturedOperation);
        Assert.Equal(DistributedApplicationOperation.Publish, capturedOperation);
    }

    #region Helpers

    private static (TestPublishTarget target, TestConnectionPoints connectionPoints) CreateTestTarget(ILogger? logger = null)
    {
        logger ??= Mock.Of<ILogger>();
        var target = new TestPublishTarget(logger);
        var connectionPoints = new TestConnectionPoints();
        return (target, connectionPoints);
    }

    /// <summary>
    /// Minimal test resource implementing IResource.
    /// </summary>
    private class TestResource(string name) : IResource
    {
        public string Name => name;
        public ResourceAnnotationCollection Annotations { get; } = new();
    }

    /// <summary>
    /// Test IValueProvider that returns a configurable value.
    /// </summary>
    private class TestValueProvider(string? value) : IValueProvider
    {
        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<string?>(value);
        }
    }

    /// <summary>
    /// Test IValueProvider that throws when resolved.
    /// </summary>
    private class FailingValueProvider : IValueProvider
    {
        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Cannot resolve this value during publish");
        }
    }

    /// <summary>
    /// Test connection points with simple dictionary-backed environment variables.
    /// </summary>
    private class TestConnectionPoints : AbstractCDKConstructConnectionPoints
    {
        private IDictionary<string, string>? _environmentVariables = new Dictionary<string, string>();

        public override IDictionary<string, string>? EnvironmentVariables
        {
            get => _environmentVariables;
            set => _environmentVariables = value;
        }
    }

    /// <summary>
    /// Concrete subclass of AbstractAWSPublishTarget that exposes ProcessRelationShipsAsync for testing.
    /// </summary>
    private class TestPublishTarget(ILogger logger) : AbstractAWSPublishTarget(logger)
    {
        public override string PublishTargetName => "Test";
        public override Type PublishTargetAnnotation => typeof(object);

        public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource,
            IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
        {
            throw new NotImplementedException();
        }

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Public wrapper to test the protected ProcessRelationShipsAsync method.
        /// </summary>
        public Task TestProcessRelationShipsAsync(AbstractCDKConstructConnectionPoints referencePoints, IResource resource)
        {
            return ProcessRelationShipsAsync(referencePoints, resource);
        }
    }

    #endregion
}
