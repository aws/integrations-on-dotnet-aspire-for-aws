// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.AWS.Deployment.CDKDefaults;

/// <summary>
/// The base class for AWS CDK defaults providers. It provides overridable accessors and methods for all defaults values and constructs used
/// during AWS CDK construct generation for Aspire resources. Users must use a version derived class or their own subsclass. The version derived
/// class like <see cref="CDKDefaultsProviderPreviewV1"/> provides a locked snapshot of default values. As defaults evolve over time new 
/// versioned subclasses will be provided allowing users to opt in to the defaults when ready. 
/// <para>
/// Users may also create their own subclass to provide custom default values. This is useful when new versions have some changes that want to adopt
/// but other changes they are not ready to adopt. By creating their own subclass users can cherry pick which defaults to override.
/// </para>
/// </summary>
[Experimental(Constants.ASPIREAWSPUBLISHERS001)]
public abstract partial class CDKDefaultsProvider
{
    /// <summary>
    /// Gets the AWS CDK environment resource associated with this instance.
    /// </summary>
    private AWSCDKEnvironmentResource EnvironmentResource { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="CDKDefaultsProvider"/> class.
    /// </summary>
    /// <param name="environmentResource">The <see cref="AWSCDKEnvironmentResource"/> the CDKDefaultsProvider will create defaults for.</param>
    protected CDKDefaultsProvider(AWSCDKEnvironmentResource environmentResource)
    {
        EnvironmentResource = environmentResource;
    }
}

