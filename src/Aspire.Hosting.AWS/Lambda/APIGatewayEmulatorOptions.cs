// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

namespace Aspire.Hosting.AWS.Lambda;

/// <summary>
/// Options that can be added to the API Gateway emulator resource.
/// </summary>
public class APIGatewayEmulatorOptions
{
    public APIGatewayEmulatorOptions()
    {
        DisableHttpsEndpoint = !Utils.DevCertificateDetector.HasAspNetCoreDevCert();
    }

    /// <summary>
    /// The HTTP port that the API Gateway emulator will listen on. If not set, a random port will be used.
    /// </summary>
    public int? HttpPort { get; set; } = null;

    /// <summary>
    /// The HTTPS port that the API Gateway emulator will listen on. If not set, a random port will be used.
    /// </summary>
    public int? HttpsPort { get; set; } = null;

    /// <summary>
    /// Setting to true will disable the creation of the HTTPS endpoint for the API Gateway emulator.
    /// </summary>
    public bool DisableHttpsEndpoint { get; set; } = false;
}
