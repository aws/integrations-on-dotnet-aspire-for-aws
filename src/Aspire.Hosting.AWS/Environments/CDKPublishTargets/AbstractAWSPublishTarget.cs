// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using IResource = Aspire.Hosting.ApplicationModel.IResource;

namespace Aspire.Hosting.AWS.Environments.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AbstractAWSPublishTarget(ILogger logger) : IAWSPublishTarget
{
    public abstract string PublishTargetName { get; }
    public abstract Type PublishTargetAnnotation { get; }

    public abstract Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken);
    public abstract IList<KeyValuePair<string, string>>? GetReferences(IResource resource, IConstruct resourceConstruct);
    public abstract IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(DefaultProvider defaultProvider, IResource resource);

    protected void ApplyRelationshipEnvironmentVariable(IDictionary<string, string> environmentVariables, IResource resource)
    {
        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotations>(out var targetLinkedConstructAnnotation))
                continue;

            var references = targetLinkedConstructAnnotation.PublishTarget.GetReferences(relatedAnnotation.Resource, targetLinkedConstructAnnotation.LinkedConstruct);
            if (references != null)
            {
                foreach (var reference in references)
                {
                    environmentVariables[reference.Key] = reference.Value;
                }
            }
            else
            {
                logger.LogWarning("No references found for relationship from resource {ResourceName} to {RelatedResourceName} using publish target {PublishTargetName}", resource.Name, relatedAnnotation.Resource.Name, targetLinkedConstructAnnotation.PublishTarget.PublishTargetName);
            }
        }
    }

    protected void ApplyLinkedConstructAnnotation(IResource resource, Construct sourceConstruct, IAWSPublishTarget publishTarget)
    {
        resource.Annotations.Add(new LinkedConstructAnnotations { LinkedConstruct = sourceConstruct, PublishTarget = publishTarget });

        var relatedAnnotations = resource.Annotations.OfType<ResourceRelationshipAnnotation>();
        foreach (var relatedAnnotation in relatedAnnotations)
        {
            if (relatedAnnotation.Type != "Reference" || !relatedAnnotation.Resource.TryGetLastAnnotation<LinkedConstructAnnotations>(out var targetLinkedConstructAnnotation))
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
                Tags.Of(scope).Add(environment.DefaultValuesProvider.DeploymentTagName, tag);
            }
        }
    }
}
