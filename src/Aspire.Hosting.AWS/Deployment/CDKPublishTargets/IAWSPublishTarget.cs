// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public interface IAWSPublishTarget
{
    /// <summary>
    /// The name of the type publish target that will be created. This is used for logging and diagnostic purposes.
    /// </summary>
    string PublishTargetName { get; }

    /// <summary>
    /// The Aspire annotations type that this publish target can process.
    /// </summary>
    Type PublishTargetAnnotation { get; }

    /// <summary>
    /// Generates the AWS CDK construct(s) for the given Aspire resource.
    /// </summary>
    /// <param name="environment">The <see cref="AWSCDKEnvironmentResource"/> the owns the published resource</param>
    /// <param name="resource">The Aspire resource that will be transformed into CDK construct(s)</param>
    /// <param name="publishAnnotation">The instance of the publishing annotation attached to the Aspire resource</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Task to await for completion</returns>
    Task GenerateConstructAsync(AWSCDKEnvironmentResource environment, IResource resource, IAWSPublishTargetAnnotation publishAnnotation, CancellationToken cancellationToken);

    /// <summary>
    /// For the resource associated with the given linked annotation, gets the connection information needed to reference the resource once deployed to AWS.
    /// </summary>
    /// <param name="linkedAnnotation">The <see cref="AWSLinkedObjectsAnnotation"/> containing the mapped objects for converting the Aspire resource into CDK construct</param>
    /// <returns>The reference connection info that can be applied to another resource to allow the resource to connect.</returns>

    ReferenceConnectionInfo GetReferenceConnectionInfo(AWSLinkedObjectsAnnotation linkedAnnotation);

    /// <summary>
    /// Returns true if the reference requires the resource that needs to connect to this resource be in the same VPC.
    /// For example a Lambda function that normally doesn't need to be in a VPC connecting to an ElastiCache cluster would need to be in the same VPC.
    /// </summary>
    /// <returns></returns>
    bool ReferenceRequiresVPC();

    /// <summary>
    /// Returns true if the reference requires a security group to security group ingress will to be created.
    /// </summary>
    /// <returns></returns>
    bool ReferenceRequiresSecurityGroup();

    /// <summary>
    /// Adds to the CDK construct for the resource in the a security group ingress rule to allow the resource owning the security group network access to the resource.
    /// </summary>
    /// <param name="linkedAnnotation">The container of the Aspire resource and CDK construct that where the ingress will be created</param>
    /// <param name="securityGroup">The security group that will be added as a ingress rule to the resource</param>
    void ApplyReferenceSecurityGroup(AWSLinkedObjectsAnnotation linkedAnnotation, ISecurityGroup securityGroup);

    /// <summary>
    /// For Aspire resources that do not have an explicit publish target added this method is called to see if the publish target is the best 
    /// match for the resource.
    /// </summary>
    /// <param name="cdkDefaultsProvider">The defaults provider that contains information on what default targets should be used for the type or project and resource</param>
    /// <param name="resource">The resource to evaluate for best match</param>
    /// <returns>The results of the evaluation</returns>
    IsDefaultPublishTargetMatchResult IsDefaultPublishTargetMatch(CDKDefaultsProvider cdkDefaultsProvider, IResource resource);
}

public class IsDefaultPublishTargetMatchResult
{
    public const int DEFAULT_MATCH_RANK = 100;

    public static readonly IsDefaultPublishTargetMatchResult NO_MATCH = new IsDefaultPublishTargetMatchResult { IsMatch = false };

    public bool IsMatch { get; init; }

    public IResourceAnnotation? PublishTargetAnnotation { get; init; }

    public int Rank { get; init; } = DEFAULT_MATCH_RANK;
}

public class ReferenceConnectionInfo
{
    public IDictionary<string, string>? EnvironmentVariables { get; set; }
}
