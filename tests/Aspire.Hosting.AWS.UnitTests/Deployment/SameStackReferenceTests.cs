// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.IAM;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.CDKPublishTargets;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aspire.Hosting.AWS.UnitTests.Deployment;

[Collection("CDKDeploymentTests")]
public class SameStackReferenceTests
{
    [Fact]
    public void QueueReferenceUsesConstructTokenAndGrantsSendAndConsumePermissions()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });
        var environment = builder.AddAWSCDKEnvironment("environment", CDKDefaultsProviderFactory.Preview_V1);
        var queue = environment.UseDeploymentStack().AddSQSQueue("queue");
        var service = builder.AddContainer("service", "image").WithReference(queue);
        var role = new Role(environment.Resource.CDKStack, "service-role", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
        });
        var connectionPoints = new TestConnectionPoints(role);

        new TestPublishTarget().Process(connectionPoints, service.Resource, environment.Resource);

        Assert.Equal(
            queue.Resource.Construct.QueueUrl,
            connectionPoints.EnvironmentVariables!["AWS__Resources__queue__QueueUrl"]);

        var assembly = ((Amazon.CDK.App)environment.Resource.CDKStack.Node.Root).Synth();
        var template = assembly.GetStackArtifact(environment.Resource.CDKStack.ArtifactId).TemplateFullPath;
        var json = File.ReadAllText(template);
        Assert.Contains("sqs:SendMessage", json, StringComparison.Ordinal);
        Assert.Contains("sqs:ReceiveMessage", json, StringComparison.Ordinal);
        Assert.Contains("sqs:DeleteMessage", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueEventSourceCreatesLambdaMappingAndConsumePolicy()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"aspire-aws-lambda-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);
        File.WriteAllText(Path.Combine(bundlePath, "bootstrap"), "test");

        try
        {
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = ["--operation", "publish", "--step", "publish"]
            });
            var environment = builder.AddAWSCDKEnvironment("environment", CDKDefaultsProviderFactory.Preview_V1);
            var queue = environment.UseDeploymentStack().AddSQSQueue("queue");
            var lambda = builder.AddResource(new LambdaProjectResource("function"))
                .WithSQSEventSource(queue, new() { BatchSize = 5 });

            var annotation = Assert.Single(lambda.Resource.Annotations.OfType<LambdaSQSEventSourceAnnotation>());
            Assert.Same(queue.Resource.Construct, annotation.Queue);
            Assert.Same(queue.Resource.Parent, annotation.Stack);
            Assert.Equal(5, annotation.Options?.BatchSize);

            lambda.Resource.Annotations.Add(new LambdaFunctionAnnotation("assembly::type::method")
            {
                DeploymentBundlePath = bundlePath
            });
            var publishAnnotation = new PublishLambdaFunctionAnnotation
            {
                Config = new PublishLambdaFunctionConfig
                {
                    PropsFunctionCallback = (_, props) => props.Runtime = Amazon.CDK.AWS.Lambda.Runtime.DOTNET_10
                }
            };
            await new LambdaFunctionPublishTarget(NullLogger<LambdaFunctionPublishTarget>.Instance)
                .GenerateConstructAsync(environment.Resource, lambda.Resource, publishAnnotation, default);

            var assembly = ((Amazon.CDK.App)environment.Resource.CDKStack.Node.Root).Synth();
            var template = File.ReadAllText(assembly.GetStackArtifact(environment.Resource.CDKStack.ArtifactId).TemplateFullPath);
            Assert.Contains("AWS::Lambda::EventSourceMapping", template, StringComparison.Ordinal);
            Assert.Contains("\"BatchSize\": 5", template, StringComparison.Ordinal);
            Assert.Contains("sqs:ReceiveMessage", template, StringComparison.Ordinal);
            Assert.Contains("sqs:DeleteMessage", template, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(bundlePath, recursive: true);
        }
    }

    private sealed class TestConnectionPoints(IRole role) : AbstractCDKConstructConnectionPoints
    {
        public override IDictionary<string, string>? EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public override IRole? ReferenceTaskRole => role;
    }

    private sealed class TestPublishTarget() : AbstractAWSPublishTarget(NullLogger.Instance)
    {
        public void Process(AbstractCDKConstructConnectionPoints points, Aspire.Hosting.ApplicationModel.IResource resource, AWSCDKEnvironmentResource environment) =>
            ProcessRelationShips(points, resource, environment);

        public override string PublishTargetName => "test";

        public override Type PublishTargetAnnotation => typeof(TestPublishAnnotation);

        public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, Aspire.Hosting.ApplicationModel.IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation) => new();

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, Aspire.Hosting.ApplicationModel.IResource resource) =>
            IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    private sealed class TestPublishAnnotation : IAWSPublishTargetAnnotation;
}
