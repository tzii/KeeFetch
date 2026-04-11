using System;

namespace KeeFetch.IconSelection
{
    internal sealed class ProviderCapabilities
    {
        public ProviderCapabilities(string providerName, IconTier defaultTier,
            bool isThirdParty, bool isSyntheticCapable, bool isPlaceholderProne,
            int concurrencyCap, double baseConfidence, bool allowPrivateHosts)
        {
            ProviderName = providerName ?? string.Empty;
            DefaultTier = defaultTier;
            IsThirdParty = isThirdParty;
            IsSyntheticCapable = isSyntheticCapable;
            IsPlaceholderProne = isPlaceholderProne;
            ConcurrencyCap = Math.Max(1, concurrencyCap);
            BaseConfidence = Math.Max(0.0, Math.Min(1.0, baseConfidence));
            AllowPrivateHosts = allowPrivateHosts;
        }

        public string ProviderName { get; private set; }
        public IconTier DefaultTier { get; private set; }
        public bool IsThirdParty { get; private set; }
        public bool IsSyntheticCapable { get; private set; }
        public bool IsPlaceholderProne { get; private set; }
        public int ConcurrencyCap { get; private set; }
        public double BaseConfidence { get; private set; }
        public bool AllowPrivateHosts { get; private set; }
    }
}
