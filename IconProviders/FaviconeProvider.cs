using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class FaviconeProvider : IconProviderBase
    {
        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Favicone", IconTier.SyntheticFallback,
                isThirdParty: true, isSyntheticCapable: true, isPlaceholderProne: true,
                concurrencyCap: 2, baseConfidence: 0.40, allowPrivateHosts: false);

        public override string Name { get { return "Favicone"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            int size = Math.Max(16, Math.Min(256, request.MaxIconSize));
            return string.Format("https://favicone.com/{0}?s={1}",
                Uri.EscapeDataString(request.TargetHost), size);
        }
    }
}
