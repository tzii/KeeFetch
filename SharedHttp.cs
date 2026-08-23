using System.Net;
using System.Net.Http;

namespace KeeFetch
{
    /// <summary>
    /// Provides a shared HttpClient instance for all HTTP operations.
    /// Uses the system default proxy configuration.
    /// </summary>
    internal static class SharedHttp
    {
        private static HttpClient client = CreateClient();

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
            return new HttpClient(handler);
        }

        public static HttpClient Instance
        {
            get { return client; }
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
