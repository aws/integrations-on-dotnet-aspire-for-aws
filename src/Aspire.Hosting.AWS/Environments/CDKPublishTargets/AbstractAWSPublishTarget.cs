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

    public virtual void ApplyReferenceSecurityGroup(LinkedConstructAnnotation linkedAnnotation, ISecurityGroup securityGroup)
    {
        
    }

    protected IList<GetReferencesResult> GetAllReferences(IResource resource)
    {
        var references = new List<GetReferencesResult>();
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotation>(out var targetLinkedConstructAnnotation))
                continue;            
            
            var result = targetLinkedConstructAnnotation.PublishTarget.GetAllReferences(relatedAnnotation.Resource, targetLinkedConstructAnnotation.LinkedConstruct);
            references.Add(result);
        }

        return references;
    }

    protected IList<LinkedConstructAnnotation> GetAllReferencesLink(IResource resource)
    {
        var links = new List<LinkedConstructAnnotation>();
        
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotation>(out var targetLinkedConstructAnnotation))
                continue;            
            
            links.Add(targetLinkedConstructAnnotation);
        }        

        return links;
    }

    protected void ApplyLinkedConstructAnnotation(AWSCDKEnvironmentResource environmentResource, IResource resource, Construct sourceConstruct, IAWSPublishTarget publishTarget)
    {
        resource.Annotations.Add(new LinkedConstructAnnotation { EnvironmentResource = environmentResource, Resource = resource, LinkedConstruct = sourceConstruct, PublishTarget = publishTarget });

        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotation>(out var targetLinkedConstructAnnotation))
                continue;

            sourceConstruct.Node.AddDependency(targetLinkedConstructAnnotation.LinkedConstruct);
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
