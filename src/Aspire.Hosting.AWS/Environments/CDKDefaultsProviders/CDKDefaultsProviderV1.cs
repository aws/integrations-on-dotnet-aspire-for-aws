using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Environments.CDKDefaultsProviders;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class V1DefaultProvider(AWSCDKEnvironmentResource environmentResource) : CDKDefaultsProvider(environmentResource)
{
}
