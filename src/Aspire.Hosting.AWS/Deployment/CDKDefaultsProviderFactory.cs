using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.AWS.Deployment.CDKDefaults;

namespace Aspire.Hosting.AWS.Deployment;

/// <summary>
/// Factory for creating the CDKDefaultsProvider. Most usages should use the static instances provided like <see cref="CDKDefaultsProviderFactory.Preview_V1"/> unless creating
/// a custom subclass of the provided CDKDefaultsProvider implementations.
/// </summary>
/// <param name="factory"></param>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public class CDKDefaultsProviderFactory(Func<AWSCDKEnvironmentResource, CDKDefaultsProvider> factory)
{
    /// <summary>
    /// Preview V1 implementation of the CDKDefaultsProvider.
    /// </summary>
    public static readonly CDKDefaultsProviderFactory Preview_V1 = new((environment) => new CDKDefaultsProviderPreviewV1(environment));

    /// <summary>
    /// Construct the <see cref="CDKDefaultsProvider"> with the provided <see cref="AWSCDKEnvironmentResource"/>
    /// </summary>
    /// <param name="environment">The <see cref="AWSCDKEnvironmentResource"/> used as the parent for the <see cref="CDKDefaultsProvider"/></param>
    /// <returns>The <see cref="CDKDefaultsProvider"/> used from providing the default values and constructs used for publishing and deploying</returns>
    public CDKDefaultsProvider Create(AWSCDKEnvironmentResource environment)
    {
        return factory(environment);
    }
}