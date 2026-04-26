using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class YandexProvider : IconProviderBase
    {
        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Yandex", IconTier.StrongResolved,
                isThirdParty: true, isSyntheticCapable: false, isPlaceholderProne: false,
                concurrencyCap: 2, baseConfidence: 0.70, allowPrivateHosts: false);

        public override string Name { get { return "Yandex"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            int size = Math.Max(16, Math.Min(256, request.MaxIconSize));

            string resolvedSize = "l";
            if (size <= 16) resolvedSize = "s";
            else if (size <= 32) resolvedSize = "m";

            return string.Format(
                "https://favicon.yandex.net/favicon/{0}?size={1}",
                Uri.EscapeDataString(request.TargetHost), resolvedSize);
        }
    }
}
