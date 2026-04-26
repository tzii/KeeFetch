using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class GoogleProvider : IconProviderBase
    {
        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Google", IconTier.StrongResolved,
                isThirdParty: true, isSyntheticCapable: false, isPlaceholderProne: false,
                concurrencyCap: 2, baseConfidence: 0.74, allowPrivateHosts: false);

        public override string Name { get { return "Google"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            int size = Math.Max(16, Math.Min(256, request.MaxIconSize));
            return string.Format(
                "https://www.google.com/s2/favicons?domain={0}&sz={1}",
                Uri.EscapeDataString(request.TargetHost), size);
        }
    }
}
