// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public partial class CDKDefaultsProvider
{
    protected AWSCDKEnvironmentResource EnvironmentResource { get; }

    protected CDKDefaultsProvider(AWSCDKEnvironmentResource environmentResource)
    {
        EnvironmentResource = environmentResource;
    }

    public virtual string DeploymentTagName => "aspire:deployment-tag";
}

