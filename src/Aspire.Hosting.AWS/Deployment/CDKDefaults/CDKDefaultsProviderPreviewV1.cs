// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

/// <summary>
/// Preview V1 of the AWS CDK defaults provider.
/// <para>Default Compute Services:</para>
/// <list type="table">
///     <listheader>
///         <term>Resource Type</term>
///         <description>Default Compute Service</description>
///     </listheader>
///     <item>
///         <term>Web Applications</term>
///         <description>ECS Fargate Express Service</description>
///     </item>
///     <item>
///         <term>Console Applications</term>
///         <description>ECS Fargate Service</description>
///     </item>
///     <item>
///         <term>Lambda Functions</term>
///         <description>AWS Lambda</description>
///     </item>
///     <item>
///         <term>Redis or Valkey Resource</term>
///         <description>ElastiCache Serverless Cluster</description>
///     </item>
/// </list>
/// </summary>
/// <param name="environmentResource">The <see cref="AWSCDKEnvironmentResource"/> the CDKDefaultsProvider will create defaults for.</param>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKDefaultsProviderPreviewV1(AWSCDKEnvironmentResource environmentResource) : CDKDefaultsProvider(environmentResource)
{
}
