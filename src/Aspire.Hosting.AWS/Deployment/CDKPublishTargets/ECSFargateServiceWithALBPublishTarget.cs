// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using Aspire.Hosting.AWS.Deployment.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateServiceWithALBPublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServiceWithALBPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate";

    public override Type PublishTargetAnnotation => typeof(PublishECSFargateServiceWithALBAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishECSFargateServiceWithALBAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishECSFargateServiceWithALBAnnotation)}.");

        var imageTarballPath = await imageBuilder.CreateTarballImageAsync(projectResource, cancellationToken);

        var taskImageOptions = new ApplicationLoadBalancedTaskImageOptions
        {
            Image = ContainerImage.FromTarball(imageTarballPath),
            Environment = new Dictionary<string, string>()
        };

        publishAnnotation.Config.PropsApplicationLoadBalancedTaskImageOptionsCallback?.Invoke(CreatePublishTargetContext(environment), taskImageOptions);
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(taskImageOptions);

        var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
        {
            TaskImageOptions = taskImageOptions
        };
        publishAnnotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(CreatePublishTargetContext(environment), fargateServiceProps);
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(fargateServiceProps);

        var referencePoints = new ApplicationLoadBalancedFargateServicePropsConnectionPoints(
            fargateServiceProps,
                () => CreateEmptyReferenceSecurityGroup(environment, projectResource, fargateServiceProps, x => x.SecurityGroups, (x, v) => x.SecurityGroups = v));
        ProcessRelationShips(referencePoints, projectResource);

        var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(CreatePublishTargetContext(environment), fargateService);
        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, fargateService, this);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource)
    {
        if (resource is ProjectResource projectResource &&
            projectResource.GetEndpoints().Any() &&
            cdkDefaultsProvider.DefaultWebProjectResourcePublishTarget == CDKDefaultsProvider.WebProjectResourcePublishTarget.ECSFargateServiceWithALB
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishECSFargateServiceWithALBAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100 // Override to raise rank over console application default
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new ReferenceConnectionInfo();
        if (linkedAnnotation.Construct is not ApplicationLoadBalancedFargateService albFargateConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        foreach (var listener in albFargateConstruct.LoadBalancer.Listeners)
        {
            string protocol = (int)listener.Port == 443 ? "https" : "http";

            var key = $"services__{linkedAnnotation.Resource.Name}__{protocol}__0";
            var endpoint = $"{protocol}://{Token.AsString(albFargateConstruct.LoadBalancer.LoadBalancerDnsName)}:{Token.AsString(listener.Port)}/";
            result.EnvironmentVariables[key] = endpoint;
        }

        return result;
    }
}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ApplicationLoadBalancedFargateServicePropsConnectionPoints(ApplicationLoadBalancedFargateServiceProps props, Func<ISecurityGroup> securityGroupFactory) : AbstractCDKConstructConnectionPoints
{
    ISecurityGroup? _referenceSecurityGroup;
    public override IDictionary<string, string>? EnvironmentVariables
    {
        get
        {
            if (props.TaskImageOptions?.Environment == null)
            {
                throw new InvalidOperationException("TaskImageOptions.Environment must be set for the ApplicationLoadBalancedFargateServiceProps");
            }

            return props.TaskImageOptions.Environment;
        }
        set
        {
            if (props.TaskImageOptions?.Environment == null)
            {
                throw new InvalidOperationException("TaskImageOptions.Environment must be set for the ApplicationLoadBalancedFargateServiceProps");
            }

            if (value != null)
            {
                foreach (var kvp in value)
                {
                    props.TaskImageOptions.Environment[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    public override ISecurityGroup? ReferenceSecurityGroup
    {
        get
        {
            _referenceSecurityGroup ??= securityGroupFactory();
            return _referenceSecurityGroup;
        }
    }
}
