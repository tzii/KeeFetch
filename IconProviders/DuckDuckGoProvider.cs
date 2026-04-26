using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class DuckDuckGoProvider : IconProviderBase
    {
        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("DuckDuckGo", IconTier.StrongResolved,
                isThirdParty: true, isSyntheticCapable: false, isPlaceholderProne: false,
                concurrencyCap: 2, baseConfidence: 0.78, allowPrivateHosts: false);

        public override string Name { get { return "DuckDuckGo"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            return string.Format(
                "https://icons.duckduckgo.com/ip3/{0}.ico",
                Uri.EscapeDataString(request.TargetHost));
        }
    }
}
