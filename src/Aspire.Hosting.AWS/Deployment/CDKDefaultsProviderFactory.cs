using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;

namespace Aspire.Hosting.AWS.Deployment;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKDefaultsProviderFactory(Func<AWSCDKEnvironmentResource, CDKDefaultsProvider> factory)
{
    public static readonly CDKDefaultsProviderFactory Preview_V1 = new((environment) => new CDKDefaultsProviderPreviewV1(environment));

    public CDKDefaultsProvider Create(AWSCDKEnvironmentResource environment)
    {
        return factory(environment);
    }
}