using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch
{
    /// <summary>
    /// Provides a shared HttpClient instance for all HTTP operations.
    /// Uses the system default proxy configuration.
    /// </summary>
    internal static class SharedHttp
    {
        /// <summary>Maximum redirect hops KeeFetch follows manually.</summary>
        internal const int MaxManualRedirects = 10;

        /// <summary>
        /// Protocols KeeFetch negotiates for its own connections. KeePass.exe targets
        /// an old framework, so the process default is TLS 1.0/1.1/1.2; pinning here
        /// excludes the legacy versions and allows 1.3 without touching
        /// <see cref="ServicePointManager.SecurityProtocol"/> for the whole process.
        /// Declared before <see cref="client"/>: static initializers run in textual order.
        /// </summary>
        internal static readonly SslProtocols RequiredSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

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
                // Redirects are followed manually so every hop's destination is
                // checked against the caller's private-host policy BEFORE the
                // request is issued (automatic redirects would contact the
                // destination first and only allow discarding the response).
                AllowAutoRedirect = false,
                AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate
                // Uses system default proxy (WebRequest.DefaultWebProxy)
            };
            handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;
            ApplySslProtocols(handler);
            return new HttpClient(handler);
        }

        private static void ApplySslProtocols(HttpClientHandler handler)
        {
            try
            {
                handler.SslProtocols = RequiredSslProtocols;
            }
            catch (Exception ex)
            {
                // Older runtimes reject the setter or the Tls13 flag; fall back to
                // the process default rather than fail every download.
                Logger.Warn("SharedHttp", ex);
            }
        }

        public static HttpClient Instance
        {
            get { return client; }
        }

        /// <summary>
        /// Sends a GET request and follows 3xx redirects manually. Before each
        /// hop the <paramref name="allowHop"/> policy is evaluated against the
        /// next absolute destination; a hop it rejects is never contacted.
        /// Returns the final response (caller disposes) or null when a hop was
        /// rejected or the redirect limit was exceeded.
        /// </summary>
        internal static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
            string url, Action<HttpRequestMessage> configureRequest,
            Func<Uri, bool> allowHop, string logName, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url) || allowHop == null)
                return null;

            Uri current;
            try
            {
                current = new Uri(url, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                return null;
            }

            for (int hop = 0; ; hop++)
            {
                HttpRequestMessage request;
                try
                {
                    request = new HttpRequestMessage(HttpMethod.Get, current);
                }
                catch (UriFormatException)
                {
                    return null;
                }
                if (configureRequest != null)
                    configureRequest(request);

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                }
                finally
                {
                    request.Dispose();
                }

                if (response == null)
                    return null;

                Uri next = GetRedirectTarget(response, current);
                if (next == null)
                    return response;

                response.Dispose();

                if (hop >= MaxManualRedirects)
                {
                    Logger.Warn(logName, "Redirect limit exceeded at " + current);
                    return null;
                }

                if (!allowHop(next))
                {
                    Logger.Warn(logName, "Refused redirect to disallowed host " + next.Host);
                    return null;
                }

                current = next;
            }
        }

        private static Uri GetRedirectTarget(HttpResponseMessage response, Uri baseUri)
        {
            HttpStatusCode code = response.StatusCode;
            if (code != HttpStatusCode.MovedPermanently &&
                code != HttpStatusCode.Found &&
                code != HttpStatusCode.SeeOther &&
                code != HttpStatusCode.RedirectMethod &&
                code != HttpStatusCode.TemporaryRedirect)
                return null;

            string location = response.Headers.Location != null
                ? response.Headers.Location.OriginalString
                : null;
            if (string.IsNullOrEmpty(location))
                return null;

            Uri absolute;
            try
            {
                absolute = new Uri(baseUri, location);
            }
            catch (UriFormatException)
            {
                return null;
            }
            return absolute;
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

            // The override exists for private/self-hosted servers (e.g. a router
            // on https://router.local). It must not silently disable validation for
            // public resolvers and public sites, where an on-path attacker with any
            // self-signed certificate must stay rejected.
            if (request != null && request.RequestUri != null &&
                !Util.IsPrivateHost(request.RequestUri.Host))
            {
                return false;
            }

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
