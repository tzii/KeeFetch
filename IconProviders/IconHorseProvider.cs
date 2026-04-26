using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class IconHorseProvider : IconProviderBase
    {
        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Icon Horse", IconTier.SyntheticFallback,
                isThirdParty: true, isSyntheticCapable: true, isPlaceholderProne: true,
                concurrencyCap: 2, baseConfidence: 0.35, allowPrivateHosts: false);

        public override string Name { get { return "Icon Horse"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            return string.Format(
                "https://icon.horse/icon/{0}",
                Uri.EscapeDataString(request.TargetHost));
        }
    }
}
