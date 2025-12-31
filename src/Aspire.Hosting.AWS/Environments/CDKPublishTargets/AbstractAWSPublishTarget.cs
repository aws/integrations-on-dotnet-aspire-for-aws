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
    public abstract GetReferencesResult GetAllReferences(IResource resource, IConstruct resourceConstruct);
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

    protected IList<GetReferencesResult> GetAllReferences(IResource resource)
    {
        var references = new List<GetReferencesResult>();
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<AWSLinkedObjectsAnnotation>(out var targetLinkedAnnotation))
                continue;            
            
            var result = targetLinkedAnnotation.PublishTarget.GetAllReferences(relatedAnnotation.Resource, targetLinkedAnnotation.Construct);
            references.Add(result);
        }

        return references;
    }

    protected IList<AWSLinkedObjectsAnnotation> GetAllReferencesLink(IResource resource)
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
}
