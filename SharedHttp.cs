using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace KeeFetch
{
    /// <summary>
    /// Provides a shared HttpClient instance for all HTTP operations.
    /// Uses the system default proxy configuration.
    /// </summary>
    internal static class SharedHttp
    {
        private static HttpClient client = CreateClient();

        // Reference-counted so overlapping download runs do not switch the
        // policy off underneath each other. Scoped to this client only: the
        // process-wide ServicePointManager callback is never touched, so
        // KeePass and other plugins keep normal certificate validation.
        private static int selfSignedCertScopes;

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate
                // Uses system default proxy (WebRequest.DefaultWebProxy)
            };
            handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;
            return new HttpClient(handler);
        }

        public static HttpClient Instance
        {
            get { return client; }
        }

        /// <summary>
        /// True while at least one caller has enabled self-signed certificate acceptance.
        /// </summary>
        internal static bool AllowSelfSignedCertificates
        {
            get { return Volatile.Read(ref selfSignedCertScopes) > 0; }
        }

        /// <summary>
        /// Enables or disables acceptance of self-signed / untrusted-chain certificates
        /// for requests made through <see cref="Instance"/>. Calls must be balanced.
        /// </summary>
        internal static void SetAllowSelfSignedCertificates(bool allow)
        {
            if (allow)
            {
                Interlocked.Increment(ref selfSignedCertScopes);
                return;
            }

            int remaining = Interlocked.Decrement(ref selfSignedCertScopes);
            if (remaining < 0)
                Interlocked.Exchange(ref selfSignedCertScopes, 0);
        }

        internal static bool ValidateServerCertificate(HttpRequestMessage request, X509Certificate2 certificate,
            X509Chain chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            if (!AllowSelfSignedCertificates)
                return false;

            // Accept chain/trust problems (self-signed, private CA) but never a
            // certificate issued for a different host or one that is missing.
            return (errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0 &&
                   (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0 &&
                   (errors & SslPolicyErrors.RemoteCertificateNotAvailable) == 0;
        }

        /// <summary>
        /// Test seam: swaps the shared client (e.g. for one backed by a fake
        /// transport) and disposes the previous instance. Not for production use.
        /// </summary>
        internal static void ReplaceClientForTests(HttpClient replacement)
        {
            HttpClient previous = client;
            client = replacement;
            if (previous != null && !ReferenceEquals(previous, replacement))
                previous.Dispose();
        }

        internal static void ResetClientForTests()
        {
            ReplaceClientForTests(CreateClient());
        }
    }
}
