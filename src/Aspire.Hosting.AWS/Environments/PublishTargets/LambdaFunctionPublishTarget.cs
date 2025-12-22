// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Environments;
using Aspire.Hosting.AWS.Environments.PublishTargets;
using Aspire.Hosting.AWS.Lambda;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    public class PublishCDKLambdaFunctionConfig
    {
        public Action<FunctionProps>? PropsFunctionCallback { get; set; }

        public Action<Function>? ConstructFunctionCallback { get; set; }
    }

    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class PublishCDKLambdaFunctionAnnotation : IAWSPublishTargetAnnotation
    {
        public PublishCDKLambdaFunctionConfig Config { get; init; } = new PublishCDKLambdaFunctionConfig();
    }
}

namespace Aspire.Hosting
{
    public static partial class AWSCDKEnvironmentExtensions
    {
        /// <summary>
        /// Deploy project as to AWS Lambda as a function.
        /// The CDK <a href="https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.aws_lambda.Function.html">Function</a> construct is used to create the Lambda function.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config">Configuration for attaching callbacks to configure the CDK construct's props and associate the created CDK construct to other CDK constructs.</param>
        /// <returns></returns>
        [Experimental(AWS.Constants.ASPIREAWSPUBLISHERS001)]
        public static IResourceBuilder<LambdaProjectResource> PublishAsLambdaFunction(this IResourceBuilder<LambdaProjectResource> builder, PublishCDKLambdaFunctionConfig? config = null)
        {
            var annotation = new PublishCDKLambdaFunctionAnnotation { Config = config ?? new PublishCDKLambdaFunctionConfig() };
            builder.Resource.Annotations.Add(annotation);

            return builder;
        }
    }
}

namespace Aspire.Hosting.AWS.Environments.CDKResourceContexts
{
    [Experimental(Constants.ASPIREAWSPUBLISHERS001)]
    internal class LambdaFunctionPublishTarget(ILogger<LambdaFunctionPublishTarget> logger) : AbstractAWSPublishTarget(logger)
    {
        public override string PublishTargetName => "Lambda function";

        public override Type PublishTargetAnnotation => typeof(PublishCDKLambdaFunctionAnnotation);

        public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
        {
            var projectResource = resource as ProjectResource
                ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

            var publishAnnotation = annotation as PublishCDKLambdaFunctionAnnotation
                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKLambdaFunctionAnnotation)}.");

            var lambdaFunction = resource as LambdaProjectResource;
            if (lambdaFunction == null)
            {
                throw new InvalidOperationException($"The project resource {resource.Name} is not a Lambda function.");
            }

            if (!lambdaFunction.TryGetLastAnnotation<LambdaFunctionAnnotation>(out var lambdaFunctionAnnotation))
            {
                throw new InvalidOperationException($"Missing {nameof(LambdaFunctionAnnotation)} annotation");
            }

            var functionProps = new FunctionProps
            {
                Code = Code.FromAsset(lambdaFunctionAnnotation.DeploymentBundlePath!),
                Handler = lambdaFunctionAnnotation.Handler
            };
            ProcessRelationShips(functionProps, lambdaFunction);
            publishAnnotation.Config.PropsFunctionCallback?.Invoke(functionProps);
            environment.DefaultValuesProvider.ApplyLambdaFunctionDefaults(lambdaFunction.GetProjectMetadata().ProjectPath, functionProps);

            var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
            publishAnnotation.Config.ConstructFunctionCallback?.Invoke(function);
            ApplyLinkedConstructAnnotation(lambdaFunction, function, this);

            await ApplyDeploymentTagAsync(environment, lambdaFunction, function, cancellationToken);
        }

        public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
        {
            if (resource is LambdaProjectResource &&
                defaultProvider.DefaultLambdaProjectPublishTarget == DefaultProvider.LambdaComputeService.Lambda
                )
            {
                return new IsDefaultPublishTargetMatchResult
                {
                    IsMatch = true,
                    PublishTargetAnnotation = new PublishCDKLambdaFunctionAnnotation(),
                    Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 200 // Override to raise rank over any "ProjectResource" defaults.
                };
            }

            return IsDefaultPublishTargetMatchResult.NO_MATCH;
        }

        public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
        {
            return null;
        }

        private void ProcessRelationShips(FunctionProps props, IResource resource)
        {
            if (props.Environment == null)
            {
                props.Environment = new Dictionary<string, string>();
            }

            ApplyRelationshipEnvironmentVariable(props.Environment, resource);
        }
    }
}