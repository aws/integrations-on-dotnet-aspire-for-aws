// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Environments.CDKDefaults;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AbstractAWSPublishTarget(ILogger logger) : IAWSPublishTarget
{
    protected ILogger Logger { get; } = logger;
    
    public abstract string PublishTargetName { get; }
    public abstract Type PublishTargetAnnotation { get; }

    public abstract Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken);
    public abstract GetReferencesResult GetReferences(AWSLinkedObjectsAnnotation linkedAnnotation);
    public abstract IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource);

    public virtual bool ReferenceRequiresVPC()
    {
        return false;
    }

    public virtual bool ReferenceRequiresSecurityGroup()
    {
        return false;
    }

    public virtual void ApplyReferenceSecurityGroup(AWSLinkedObjectsAnnotation linkedAnnotation, ISecurityGroup securityGroup)
    {
        
    }

    protected IList<AWSLinkedObjectsAnnotation> GetAllReferencesLinks(IResource resource)
    {
        var links = new List<AWSLinkedObjectsAnnotation>();
        
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<AWSLinkedObjectsAnnotation>(out var targetLinkedAnnotation))
                continue;            
            
            links.Add(targetLinkedAnnotation);
        }        

        return links;
    }

    protected void ApplyAWSLinkedObjectsAnnotation(AWSCDKEnvironmentResource environmentResource, IResource resource, Construct sourceConstruct, IAWSPublishTarget publishTarget)
    {
        resource.Annotations.Add(new AWSLinkedObjectsAnnotation { EnvironmentResource = environmentResource, Resource = resource, Construct = sourceConstruct, PublishTarget = publishTarget });

        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<AWSLinkedObjectsAnnotation>(out var targetLinkedAnnotation))
                continue;

            sourceConstruct.Node.AddDependency(targetLinkedAnnotation.Construct);
        }
    }

    protected async Task ApplyDeploymentTagAsync(AWSCDKEnvironmentResource environment, IResource aspireResource, IConstruct scope, CancellationToken cancellationToken)
    {
        if (aspireResource.TryGetLastAnnotation<DeploymentImageTagCallbackAnnotation>(out var deploymentTag))
        {
            var context = new DeploymentImageTagCallbackAnnotationContext
            {
                Resource = aspireResource,
                CancellationToken = cancellationToken,
            };
            var tag = await deploymentTag.Callback(context).ConfigureAwait(false);
            if (tag != null)
            {
                Tags.Of(scope).Add(environment.DefaultsProvider.DeploymentTagName, tag);
            }
        }
    }

    protected ISecurityGroup CreateEmptyReferenceSecurityGroup<T>(AWSCDKEnvironmentResource environmentResource, 
        IResource resource, T construct, Func<T, ISecurityGroup[]?> getter, Action<T, ISecurityGroup[]> setter)
    {
        var securityGroup = new SecurityGroup(
            environmentResource.CDKStack,
            $"{resource.Name}-Ref",
            new SecurityGroupProps
            {
                Vpc = environmentResource.DefaultsProvider.GetDefaultVpc(),
                Description = $"Security group for linking {resource.Name} to Aspire References",
                AllowAllOutbound = true
            });
        
        AppendSecurityGroup(construct, getter, setter, securityGroup);
        
        return securityGroup;
    }

    private void AppendSecurityGroup<T>(T construct, Func<T, ISecurityGroup[]?> getter, Action<T, ISecurityGroup[]> setter, ISecurityGroup securityGroup)
    {
        var securityGroups = getter(construct);
        
        if (securityGroups == null)
        {
            securityGroups = new ISecurityGroup[] { securityGroup };
        }
        else
        {
            var securityGroupList =  securityGroups.ToList();
            securityGroupList.Add(securityGroup);
            securityGroups = securityGroupList.ToArray();
        }
        
        setter(construct, securityGroups);
    }
    
    protected virtual void ProcessRelationShips(AbstractCDKConstructReferencePoints referencePoints, ApplicationModel.IResource resource)
    {
        var environmentVariables = referencePoints.EnvironmentVariables;
         
        var allLinkReferences = GetAllReferencesLinks(resource);
        foreach (var linkAnnotation in allLinkReferences)
        {
            var results =
                linkAnnotation.PublishTarget.GetReferences(linkAnnotation);

            if (environmentVariables != null && results.EnvironmentVariables != null)
            {
                foreach (var kvp in results.EnvironmentVariables)
                    environmentVariables[kvp.Key] = kvp.Value;  
            }

            if (linkAnnotation.PublishTarget.ReferenceRequiresVPC())
            {
                referencePoints.Vpc = linkAnnotation.EnvironmentResource.DefaultsProvider.GetDefaultVpc();
            }

            if (linkAnnotation.PublishTarget.ReferenceRequiresSecurityGroup() && referencePoints.ReferenceSecurityGroup != null)
            {
                linkAnnotation.PublishTarget.ApplyReferenceSecurityGroup(linkAnnotation, referencePoints.ReferenceSecurityGroup);
            }
        }

        if (environmentVariables != null)
        {
            referencePoints.EnvironmentVariables = environmentVariables;
        }
    }    
}
