using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Environments.CDKDefaults;

namespace Aspire.Hosting.AWS.Environments;

[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKDefaultsProviderFactory(Func<AWSCDKEnvironmentResource, CDKDefaultsProvider> factory)
{
    public static readonly CDKDefaultsProviderFactory V1 = new((environment) => new V1DefaultProvider(environment));

    public CDKDefaultsProvider Create(AWSCDKEnvironmentResource environment)
    {
        return factory(environment);
    }
}