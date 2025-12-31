// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments.CDKDefaults;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKDefaultsProviderPreviewV1(AWSCDKEnvironmentResource environmentResource) : CDKDefaultsProvider(environmentResource)
{
}
