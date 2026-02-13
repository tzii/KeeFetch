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
        private static readonly HttpClient Client = CreateClient();

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
            get { return Client; } 
        }
    }
}