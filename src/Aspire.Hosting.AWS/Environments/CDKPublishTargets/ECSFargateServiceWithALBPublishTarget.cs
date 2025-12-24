// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

#pragma warning disable ASPIREPUBLISHERS001
#pragma warning disable ASPIREAWSPUBLISHERS001

using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.Services;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class ECSFargateServiceWithALBPublishTarget(ITarballContainerImageBuilder imageBuilder, ILogger<ECSFargateServiceWithALBPublishTarget> logger) : AbstractAWSPublishTarget(logger)
{
    public override string PublishTargetName => "ECS Fargate";

    public override Type PublishTargetAnnotation => typeof(PublishCDKECSFargateServiceWithALBAnnotation);

    public override async Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation annotation, CancellationToken cancellationToken)
    {
        var projectResource = resource as ProjectResource
                              ?? throw new InvalidOperationException($"Resource {resource.Name} is not a valid IProjectResource.");

        var publishAnnotation = annotation as PublishCDKECSFargateServiceWithALBAnnotation
                                ?? throw new InvalidOperationException($"Annotation for resource {resource.Name} is not a valid {nameof(PublishCDKECSFargateServiceWithALBAnnotation)}.");

        var imageTarballPath = await imageBuilder.BuildTarballImageAsync(projectResource, cancellationToken);

        var taskImageOptions = new ApplicationLoadBalancedTaskImageOptions
        {
            Image = ContainerImage.FromTarball(imageTarballPath),
            Environment = new Dictionary<string, string>()
        };

        publishAnnotation.Config.PropsApplicationLoadBalancedTaskImageOptionsCallback?.Invoke(taskImageOptions);
        environment.DefaultValuesProvider.ApplyECSFargateServiceWithALBDefaults(taskImageOptions);

        var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
        {
            TaskImageOptions = taskImageOptions
        };
        publishAnnotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateServiceProps);
        environment.DefaultValuesProvider.ApplyECSFargateServiceWithALBDefaults(environment, fargateServiceProps);
        ProcessRelationShips(fargateServiceProps, projectResource);

        var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateService);
        ApplyLinkedConstructAnnotation(projectResource, fargateService, this);

        await ApplyDeploymentTagAsync(environment, projectResource, fargateService.Service, cancellationToken);
    }

    public override IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource)
    {
        if (resource is ProjectResource projectResource &&
            projectResource.GetEndpoints().Any() &&
            defaultProvider.DefaultWebProjectResourcePublishTarget == DefaultProvider.WebProjectResourcePublishTarget.ECSFargateServiceWithALB
           )
        {
            return new IsDefaultPublishTargetMatchResult
            {
                IsMatch = true,
                PublishTargetAnnotation = new PublishCDKECSFargateServiceWithALBAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100 // Override to raise rank over console application default
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct)
    {
        if (resourceConstruct is not ApplicationLoadBalancedFargateService albFargateConstruct)
            return null;

        var list = new List<KeyValuePair<string, string>>();

        foreach (var listener in albFargateConstruct.LoadBalancer.Listeners)
        {
            string protocol = listener.Port == 443 ? "https" : "http";

            var key = $"services__{resource.Name}__{protocol}__0";
            var endpoint = $"{protocol}://{Token.AsString(albFargateConstruct.LoadBalancer.LoadBalancerDnsName)}:{Token.AsString(listener.Port)}/";
            list.Add(new KeyValuePair<string, string>(key, endpoint));
        }

        return list.Any() ? list : null;
    }

    private void ProcessRelationShips(ApplicationLoadBalancedFargateServiceProps props, IResource resource)
    {
        if (props.TaskImageOptions?.Environment == null)
        {
            throw new InvalidOperationException("TaskImageOptions.Environment must be set for the ApplicationLoadBalancedFargateServiceProps");
        }
        ApplyRelationshipEnvironmentVariable(props.TaskImageOptions.Environment, resource);
    }
}
    
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class PublishECSFargateServiceWithALBConfig
{
    public Action<ApplicationLoadBalancedTaskImageOptions>? PropsApplicationLoadBalancedTaskImageOptionsCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateServiceProps>? PropsApplicationLoadBalancedFargateServiceCallback { get; set; }

    public Action<ApplicationLoadBalancedFargateService>? ConstructApplicationLoadBalancedFargateServiceCallback { get; set; }

}

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
internal class PublishCDKECSFargateServiceWithALBAnnotation : IAWSPublishTargetAnnotation
{
    public PublishECSFargateServiceWithALBConfig Config { get; init; } = new PublishECSFargateServiceWithALBConfig();
}