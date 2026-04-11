using System;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class TwentyIconsProvider : IconProviderBase
    {
        private static readonly int[] SupportedSizes = new[] { 16, 32, 64, 128, 180, 192 };

        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Twenty Icons", IconTier.StrongResolved,
                isThirdParty: true, isSyntheticCapable: false, isPlaceholderProne: false,
                concurrencyCap: 2, baseConfidence: 0.82, allowPrivateHosts: false);

        public override string Name { get { return "Twenty Icons"; } }
        public override ProviderCapabilities Capabilities { get { return capabilities; } }

        protected override string BuildRequestUrl(IconRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return null;

            int size = ChooseSize(request.MaxIconSize);
            return string.Format("https://twenty-icons.com/{0}/{1}",
                Uri.EscapeDataString(request.TargetHost), size);
        }

        private static int ChooseSize(int requestedSize)
        {
            int target = Math.Max(16, requestedSize);
            for (int i = 0; i < SupportedSizes.Length; i++)
            {
                if (SupportedSizes[i] >= target)
                    return SupportedSizes[i];
            }
            return SupportedSizes[SupportedSizes.Length - 1];
        }
    }
}
