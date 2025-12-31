// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Lambda;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
using Amazon.CDK.AWS.EC2;

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
        environment.DefaultsProvider.ApplyLambdaFunctionDefaults(functionProps, lambdaFunction);

        var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
        publishAnnotation.Config.ConstructFunctionCallback?.Invoke(function);
        ApplyAWSLinkedObjectsAnnotation(environment, lambdaFunction, function, this);

        await ApplyDeploymentTagAsync(environment, lambdaFunction, function, cancellationToken);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is LambdaProjectResource &&
            cdkDefaultsProvider.DefaultLambdaProjectResourcePublishTarget == CDKDefaultsProvider.LambdaProjectResourcePublishTarget.LambdaFunction
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

    public override GetReferencesResult GetAllReferences(IResource resource, IConstruct resourceConstruct)
    {
        return new GetReferencesResult();
    }

    private void ProcessRelationShips(FunctionProps props, IResource resource)
    {
        var environmentVariables = props.Environment ?? new Dictionary<string, string>();
        var allLinkReferences = GetAllReferencesLink(resource);
        foreach (var linkAnnotation in allLinkReferences)
        {
            var results =
                linkAnnotation.PublishTarget.GetAllReferences(linkAnnotation.Resource, linkAnnotation.Construct);

            if (results.EnvironmentVariables != null)
            {
                foreach (var kvp in results.EnvironmentVariables)
                {
                    environmentVariables[kvp.Key] = kvp.Value;
                }
            }

            if (results.SubnetIds != null && results.SubnetIds.Count > 0)
            {
            }

            if (linkAnnotation.PublishTarget.ReferenceRequiresVPC())
            {
                if (linkAnnotation.PublishTarget.ReferenceRequiresSecurityGroup())
                {
                    //var securityGroup = new SecurityGroup(
                    //    linkAnnotation.EnvironmentResource.CDKStack,
                    //    $"SG-{linkAnnotation.Resource.Name}-{resource.Name}",
                    //    new Amazon.CDK.AWS.EC2.SecurityGroupProps
                    //    {
                    //        Vpc = linkAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultVPC(),
                    //        Description = $"Security group for linking {resource.Name} to {linkAnnotation.Resource.Name}",
                    //        AllowAllOutbound = true
                    //    });
                    linkAnnotation.PublishTarget.ApplyReferenceSecurityGroup(linkAnnotation, linkAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultECSClusterSecurityGroup());
                }

            }
        }

        props.Environment = environmentVariables;
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