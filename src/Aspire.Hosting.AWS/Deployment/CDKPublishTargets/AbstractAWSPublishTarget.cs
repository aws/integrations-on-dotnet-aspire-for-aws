// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Constructs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;
using IResource = Aspire.Hosting.ApplicationModel.IResource;
using IValueProvider = Aspire.Hosting.ApplicationModel.IValueProvider;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

/// <summary>
/// THe base class of publish targets used to transform an Aspire resource into AWS CDK constructs.
/// </summary>
/// <param name="logger"></param>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract class AbstractAWSPublishTarget(ILogger logger) : IAWSPublishTarget
{
    protected ILogger Logger { get; } = logger;

    /// <inheritdoc/>
    public abstract string PublishTargetName { get; }

    /// <inheritdoc/>
    public abstract Type PublishTargetAnnotation { get; }


    /// <inheritdoc/>
    public abstract Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public abstract ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation);


    /// <inheritdoc/>
    public abstract IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource);

    /// <inheritdoc/>
    public virtual bool ReferenceRequiresVPC()
    {
        return false;
    }

    /// <inheritdoc/>
    public virtual bool ReferenceRequiresSecurityGroup()
    {
        return false;
    }

    /// <inheritdoc/>
    public virtual void ApplyReferenceSecurityGroup(AWSLinkedObjectsAnnotation linkedAnnotation, ISecurityGroup securityGroup)
    {
        
    }

    /// <summary>
    /// Create the <see cref="CDKPublishTargetContext"/> used for CDK props and construct callbacks.
    /// </summary>
    /// <param name="environment">The environment driving the publishing</param>
    /// <returns>The context callbacks can use to find information about the publish</returns>
    protected CDKPublishTargetContext CreatePublishTargetContext(AWSCDKEnvironmentResource environment)
    {
        return new CDKPublishTargetContext(environment.CDKStack, environment.DefaultsProvider);
    }

    /// <summary>
    /// For the given Aspire resource find all linked references to other Aspire resources.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    private IList<AWSLinkedObjectsAnnotation> GetAllReferencesLinks(IResource resource)
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

    /// <summary>
    /// After the CDK construct is created for the Aspire resource, apply an <see cref="AWSLinkedObjectsAnnotation"/> to track the relationship.
    /// </summary>
    /// <param name="environmentResource">The owning environment</param>
    /// <param name="resource">The Aspire resource being published</param>
    /// <param name="sourceConstruct">The CDK construct created for the Aspire resource</param>
    /// <param name="publishTarget">The <see cref="IAWSPublishTarget"/> used to create the CDK construct for the Aspire resource</param>
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

    /// <summary>
    /// Creates a <see cref="Amazon.CDK.AWS.EC2.ISecurityGroup"/> with no ingress rules and added it to the given construct.
    /// This is used to create security group to security group ingress rules.
    /// </summary>
    /// <typeparam name="T">The type of the CDK construct to add the security group to.</typeparam>
    /// <param name="environmentResource">The owning environment</param>
    /// <param name="resource">The Aspire resource being published</param>
    /// <param name="construct">The CDK construct mapped to the Aspire resource that will have the security group added to</param>
    /// <param name="getter">The function used to get the existing security groups from the construct</param>
    /// <param name="setter">The action used to set the security groups on the construct</param>
    /// <returns></returns>
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
            securityGroups = new [] { securityGroup };
        }
        else
        {
            var securityGroupList =  securityGroups.ToList();
            securityGroupList.Add(securityGroup);
            securityGroups = securityGroupList.ToArray();
        }
        
        setter(construct, securityGroups);
    }
    
    /// <summary>
    /// Processes all of the relationships added for the given resource.
    /// </summary>
    /// <param name="referencePoints">The CDK connection points for the given Aspire resource to add connection info for resources referencing the Aspire resource.</param>
    /// <param name="resource">The Aspire resource that potentially had other resources add a reference to.</param>
    /// <param name="environment">The CDK environment resource, used to add CloudFormation parameters for Aspire parameter references.</param>
    protected virtual void ProcessRelationShips(AbstractCDKConstructConnectionPoints referencePoints, IResource resource, AWSCDKEnvironmentResource? environment = null)
    {
        var environmentVariables = referencePoints.EnvironmentVariables;

        if (environmentVariables != null)
        {
            ApplyEnvironmentCallbackAnnotations(environmentVariables, resource, environment);
        }

        var allLinkReferences = GetAllReferencesLinks(resource);
        foreach (var linkAnnotation in allLinkReferences)
        {
            var results =
                linkAnnotation.PublishTarget.GetReferenceConnectionInfo(linkAnnotation);

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

    private void ApplyEnvironmentCallbackAnnotations(IDictionary<string, string> environmentVariables, IResource resource, AWSCDKEnvironmentResource? environment)
    {
        if (!resource.TryGetEnvironmentVariables(out var envAnnotations))
            return;

        var callbackEnv = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource,
            callbackEnv,
            cancellationToken: default);

        foreach (var annotation in envAnnotations)
        {
            try
            {
                annotation.Callback(context);
            }
            catch (Exception ex)
            {
                Logger.LogTrace("Skipping environment callback for {ResourceName}: {Message}", resource.Name, ex.Message);
            }
        }

        foreach (var kvp in callbackEnv)
        {
            var value = ResolveEnvironmentValue(kvp.Value, environment);
            if (value != null)
            {
                environmentVariables[kvp.Key] = value;
            }
        }
    }

    private string? ResolveEnvironmentValue(object value, AWSCDKEnvironmentResource? environment)
    {
        if (value is ParameterResource parameterResource && environment != null)
        {
            return GetOrCreateCfnParameter(parameterResource, environment).ValueAsString;
        }

        return value switch
        {
            string s => s,
            IManifestExpressionProvider manifest => manifest.ValueExpression,
            IValueProvider valueProvider => valueProvider.GetValueAsync(default).GetAwaiter().GetResult(),
            _ => value.ToString()
        };
    }

    private readonly Dictionary<string, CfnParameter> _cfnParameters = new();

    private CfnParameter GetOrCreateCfnParameter(ParameterResource parameterResource, AWSCDKEnvironmentResource environment)
    {
        if (_cfnParameters.TryGetValue(parameterResource.Name, out var existing))
            return existing;

        var paramId = $"Param-{parameterResource.Name}";

        if (environment.CDKStack.Node.TryFindChild(paramId) is CfnParameter existingOnStack)
        {
            _cfnParameters[parameterResource.Name] = existingOnStack;
            return existingOnStack;
        }

        var cfnParam = new CfnParameter(environment.CDKStack, paramId, new CfnParameterProps
        {
            Type = "String",
            Description = $"Parameter for Aspire resource '{parameterResource.Name}'",
            NoEcho = parameterResource.Secret
        });

        _cfnParameters[parameterResource.Name] = cfnParam;
        return cfnParam;
    }
}
