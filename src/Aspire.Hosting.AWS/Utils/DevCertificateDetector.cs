// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.Security.Cryptography.X509Certificates;

namespace Aspire.Hosting.AWS.Utils;

internal class DevCertificateDetector
{
    private const string LocalhostSubject = "CN=localhost";

    // This is the value for the Server Authentication EKU using TLS Web Server Authentication, which is required for ASP.NET Core's development certificate.
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    public static bool HasAspNetCoreDevCert()
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var now = DateTimeOffset.UtcNow;

            foreach (var cert in store.Certificates)
            {
                if (!IsLocalhost(cert))
                    continue;

                if (!IsValid(cert, now))
                    continue;

                if (!HasServerAuthEku(cert))
                    continue;

                if (!cert.HasPrivateKey)
                    continue;

                // Prefer the ASP.NET Core OID if present,
                // but don't require it for forward compatibility.
                return true;
            }

            return false;
        }
        catch
        {
            // Never throw from detection logic in a public library
            return false;
        }
    }

    private static bool IsLocalhost(X509Certificate2 cert) =>
        string.Equals(cert.Subject, LocalhostSubject, StringComparison.OrdinalIgnoreCase);

    private static bool IsValid(X509Certificate2 cert, DateTimeOffset now) =>
        now >= cert.NotBefore && now <= cert.NotAfter;

    private static bool HasServerAuthEku(X509Certificate2 cert)
    {
        foreach (var extension in cert.Extensions.OfType<X509EnhancedKeyUsageExtension>())
        {
            foreach (var oid in extension.EnhancedKeyUsages)
            {
                if (oid?.Value == ServerAuthOid)
                    return true;
            }
        }

        return false;
    }
}
