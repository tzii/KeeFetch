using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    /// <summary>
    /// Fetches a favicon for a given host from a specific source (direct site, Google, etc.).
    /// </summary>
    internal interface IIconProvider
    {
        /// <summary>Display name of this provider (used in result reporting).</summary>
        string Name { get; }
        ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Attempts to return favicon candidates for the requested target.
        /// </summary>
        Task<IReadOnlyList<IconCandidate>> GetCandidatesAsync(IconRequest request,
            CancellationToken token = default(CancellationToken));
    }
}
