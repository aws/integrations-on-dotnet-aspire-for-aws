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
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
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
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(taskImageOptions);

        var fargateServiceProps = new ApplicationLoadBalancedFargateServiceProps
        {
            TaskImageOptions = taskImageOptions
        };
        publishAnnotation.Config.PropsApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateServiceProps);
        environment.DefaultsProvider.ApplyECSFargateServiceWithALBDefaults(fargateServiceProps);
        ProcessRelationShips(environment, fargateServiceProps, projectResource);

        var fargateService = new ApplicationLoadBalancedFargateService(environment.CDKStack, $"Project-{projectResource.Name}", fargateServiceProps);
        publishAnnotation.Config.ConstructApplicationLoadBalancedFargateServiceCallback?.Invoke(fargateService);
        ApplyAWSLinkedObjectsAnnotation(environment, projectResource, fargateService, this);

        await ApplyDeploymentTagAsync(environment, projectResource, fargateService.Service, cancellationToken);
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
                PublishTargetAnnotation = new PublishCDKECSFargateServiceWithALBAnnotation(),
                Rank = IsDefaultPublishTargetMatchResult.DEFAULT_MATCH_RANK + 100 // Override to raise rank over console application default
            };
        }

        return IsDefaultPublishTargetMatchResult.NO_MATCH;
    }

    public override GetReferencesResult GetReferences(AWSLinkedObjectsAnnotation linkedAnnotation)
    {
        var result = new GetReferencesResult();
        if (linkedAnnotation.Construct is not ApplicationLoadBalancedFargateService albFargateConstruct)
            return result;

        result.EnvironmentVariables = new Dictionary<string, string>();

        foreach (var listener in albFargateConstruct.LoadBalancer.Listeners)
        {
            string protocol = listener.Port == 443 ? "https" : "http";

            var key = $"services__{linkedAnnotation.Resource.Name}__{protocol}__0";
            var endpoint = $"{protocol}://{Token.AsString(albFargateConstruct.LoadBalancer.LoadBalancerDnsName)}:{Token.AsString(listener.Port)}/";
            result.EnvironmentVariables[key] = endpoint;
        }

        return result;
    }
    
    private void ProcessRelationShips(AWSCDKEnvironmentResource environmentResource, ApplicationLoadBalancedFargateServiceProps props, ApplicationModel.IResource resource)
    {
        if (props.TaskImageOptions?.Environment == null)
        {
            throw new InvalidOperationException("TaskImageOptions.Environment must be set for the ApplicationLoadBalancedFargateServiceProps");
        }
        
        var environmentVariables = props.TaskImageOptions.Environment;
        
        ISecurityGroup? referenceSecurityGroup = null;
        var linkReferences = GetAllReferencesLinks(resource);
        foreach (var linkAnnotation in linkReferences)
        {
            var results =
                linkAnnotation.PublishTarget.GetReferences(linkAnnotation);
            
            if (results.EnvironmentVariables != null)
            {
                foreach (var kvp in results.EnvironmentVariables)
                {
                    environmentVariables[kvp.Key] = kvp.Value;
                }
            }
            
            if (linkAnnotation.PublishTarget.ReferenceRequiresSecurityGroup())
            {
                if (referenceSecurityGroup == null)
                {
                    referenceSecurityGroup = CreateEmptyReferenceSecurityGroup(linkAnnotation.EnvironmentResource, resource);
                    AppendSecurityGroup(props, x => x.SecurityGroups, (x, v) => x.SecurityGroups = v, referenceSecurityGroup);
                }
   
                linkAnnotation.PublishTarget.ApplyReferenceSecurityGroup(linkAnnotation, referenceSecurityGroup);
            }              
        }
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