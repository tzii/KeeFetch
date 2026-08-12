using System;
using System.Collections.Generic;

namespace KeeFetch.FetchProfiles
{
    internal sealed class FetchProfileDefinition
    {
        private readonly List<string> providerIds;

        public FetchProfileDefinition(
            string id,
            string displayName,
            string description,
            string intendedUse,
            IEnumerable<string> providerIds,
            int primaryTimeoutMs,
            int fallbackTimeoutMs,
            int cumulativeTimeoutMs,
            bool allowSyntheticFallbacks,
            bool isVisible,
            string evidenceReport)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            IntendedUse = intendedUse;
            PrimaryTimeoutMs = primaryTimeoutMs;
            FallbackTimeoutMs = fallbackTimeoutMs;
            CumulativeTimeoutMs = cumulativeTimeoutMs;
            AllowSyntheticFallbacks = allowSyntheticFallbacks;
            IsVisible = isVisible;
            EvidenceReport = evidenceReport;

            this.providerIds = new List<string>();
            if (providerIds != null)
            {
                foreach (string pid in providerIds)
                    this.providerIds.Add(pid);
            }
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string IntendedUse { get; private set; }
        public IList<string> ProviderIds { get { return providerIds.AsReadOnly(); } }
        public int PrimaryTimeoutMs { get; private set; }
        public int FallbackTimeoutMs { get; private set; }
        public int CumulativeTimeoutMs { get; private set; }
        public bool AllowSyntheticFallbacks { get; private set; }
        public bool IsVisible { get; private set; }
        public string EvidenceReport { get; private set; }
    }
}
