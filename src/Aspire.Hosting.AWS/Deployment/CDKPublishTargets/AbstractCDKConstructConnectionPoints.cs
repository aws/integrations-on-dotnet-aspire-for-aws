using System.Diagnostics.CodeAnalysis;
using Amazon.CDK.AWS.EC2;

namespace Aspire.Hosting.AWS.Deployment.CDKPublishTargets;

/// <summary>
/// To setup connections across constructs, environment variables and security groups have
/// to be manipulated. Each CDK construct has a different way to set this connection points.
/// The <see cref="AbstractCDKConstructConnectionPoints"/> base class is used to provide a common
/// API for setting the connection points.
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class AbstractCDKConstructConnectionPoints
{
    /// <summary>
    /// Gets and sets the environment variables on the CDK construct. The
    /// environment variable collection is used to add the environment variable
    /// to the connection string or http(s) endpoint of the resource being referenced. 
    /// </summary>
    public virtual IDictionary<string, string>? EnvironmentVariables { get; set; }

    /// <summary>
    /// Gets the security group defined on the construct used for references. The security group
    /// is added to references as an ingress rule. For example if the construct
    /// is a Lambda function and the reference is an ElastiCache cluster the
    /// Lambda function needs a security group that is added to the ElastiCache's
    /// default security group as an ingress rule.
    /// </summary>
    public virtual ISecurityGroup? ReferenceSecurityGroup { get; } = null;
    
    /// <summary>
    /// Gets and sets the VPC for the construct. This is should be overriden when VPC configuration is optional
    /// like a Lambda function. If a reference requires the construct to be in the VPC this property will set the
    /// VPC for the construct to the default VPC configured for the application. For example if a Lambda function
    /// has a reference to an ElastiCache cluster then the Lambda function must have it's Vpc property set.
    /// </summary>
    public virtual IVpc? Vpc { get; set; }
}