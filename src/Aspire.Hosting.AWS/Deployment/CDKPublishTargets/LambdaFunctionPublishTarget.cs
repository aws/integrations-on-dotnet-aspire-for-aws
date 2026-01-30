// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Lambda;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Lambda;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class LambdaFunctionPublishTarget(ILogger<LambdaFunctionPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "Lambda function";

    public override Type PublishTargetAnnotation => typeof(PublishLambdaFunctionAnnotation);

    public override Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
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

        var referencePoints = new FunctionPropsConnectionPoints(
            functionProps,
            () => CreateEmptyReferenceSecurityGroup(environment, resource, functionProps, x => x.SecurityGroups,
                (x, v) => x.SecurityGroups = v), resource.Name);

        ProcessRelationShips(referencePoints, lambdaFunction);

        publishAnnotation.Config.PropsFunctionCallback?.Invoke(CreatePublishTargetContext(environment), functionProps);
        environment.DefaultsProvider.ApplyLambdaFunctionDefaults(functionProps, lambdaFunction);

        var function = new Function(environment.CDKStack, $"Function-{lambdaFunction.Name}", functionProps);
        publishAnnotation.Config.ConstructFunctionCallback?.Invoke(CreatePublishTargetContext(environment), function);
        ApplyAWSLinkedObjectsAnnotation(environment, lambdaFunction, function, this);

        return Task.CompletedTask;
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

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        return new ReferenceConnectionInfo();
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class FunctionPropsConnectionPoints(FunctionProps props, Func<ISecurityGroup> securityGroupFactory, string resourceName) : AbstractCDKConstructConnectionPoints
{
    ISecurityGroup? _referenceSecurityGroup;
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get => props.Environment ?? new Dictionary<string, string>();
        set => props.Environment = value ?? new Dictionary<string, string>();
    }

    public override ISecurityGroup? ReferenceSecurityGroup
    {
        get
        {
            _referenceSecurityGroup ??= securityGroupFactory();
            return _referenceSecurityGroup;
        }
    }

    public override IVpc? Vpc
    {
        get => props.Vpc;
        set
        {
            if ((value?.PrivateSubnets?.Any() ?? false) == false && props.AllowPublicSubnet.GetValueOrDefault() == false)
            {
                // CDK will throw an error later, but we want to provide a more helpful message.
                var errorMessage = $"""
Lambda function {resourceName} references a resource that requires the function to be attached to a VPC. The configured 
VPC contains only public subnets and no private subnets. Lambda functions placed in public subnets are not assigned 
public IP addresses and therefore cannot directly access the internet.

To allow internet and AWS API access through a NAT Gateway, Lambda functions must be attached to private subnets. 
If the Lambda function only accesses resources within the VPC, this error can be overridden by setting the 
AllowPublicSubnet property on FunctionProps to true.
""";
                throw new InvalidOperationException(errorMessage);
            }

            props.Vpc = value;
        }
    }
}
