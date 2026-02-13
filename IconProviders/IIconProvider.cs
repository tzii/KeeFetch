using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    /// <summary>
    /// Fetches a favicon for a given host from a specific source (direct site, Google, etc.).
    /// </summary>
    internal interface IIconProvider
    {
        /// <summary>Display name of this provider (used in result reporting).</summary>
        string Name { get; }

        /// <summary>
        /// Attempts to download a favicon for <paramref name="host"/>.
        /// Returns raw image bytes on success, or null if the icon could not be obtained.
        /// </summary>
        Task<byte[]> GetIconAsync(string host, int size, int timeoutMs,
            CancellationToken token = default(CancellationToken));
    }
}
