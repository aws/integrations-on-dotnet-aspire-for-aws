// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class LambdaFunctionPublishTarget(ILogger<LambdaFunctionPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "Lambda function";

    public override Type PublishTargetAnnotation => typeof(PublishLambdaFunctionAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var lambdaFunction = resource as LambdaProjectResource
                             ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid LambdaProjectResource.");

        var publishAnnotation = annotation as PublishLambdaFunctionAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishLambdaFunctionAnnotation)}.");

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
            defaultProvider.DefaultLambdaProjectPublishTarget == DefaultProvider.LambdaProjectPublishTarget.Lambda
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishLambdaFunctionAnnotation(),
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
        props.Environment ??= new Dictionary<string, string>();

        ApplyRelationshipEnvironmentVariable(props.Environment, resource);
    }
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishLambdaFunctionConfig
{
    public Action<FunctionProps>? PropsFunctionCallback { get; set; }

    public Action<Function>? ConstructFunctionCallback { get; set; }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishLambdaFunctionAnnotation : IAWSPublishTargetAnnotation
{
    public PublishLambdaFunctionConfig Config { get; init; } = new PublishLambdaFunctionConfig();
}