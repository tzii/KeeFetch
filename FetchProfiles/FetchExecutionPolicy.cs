using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace KeeFetch.FetchProfiles
{
    /// <summary>
    /// Immutable description of every behavior-affecting execution property for a
    /// fetch: the ordered provider chain, the per-provider timeout budgets, the
    /// cumulative pipeline budget, the synthetic-fallback policy, and the
    /// early-stop policy. Managed catalog profiles and benchmark candidates both
    /// resolve to this single representation before execution so that a winning
    /// candidate behaves identically once it becomes a managed profile.
    /// </summary>
    internal sealed class FetchExecutionPolicy
    {
        internal const int DefaultPrimaryTimeoutMs = 10000;
        internal const int DefaultFallbackTimeoutMs = 5000;
        internal const int DefaultCumulativeTimeoutMs = 45000;

        private readonly List<string> providerIds;

        public FetchExecutionPolicy(
            IEnumerable<string> providerIds,
            int primaryTimeoutMs,
            int fallbackTimeoutMs,
            int cumulativeTimeoutMs,
            bool allowSyntheticFallbacks,
            bool stopAfterStrongResolved)
        {
            this.providerIds = new List<string>();
            if (providerIds != null)
            {
                foreach (string id in providerIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        this.providerIds.Add(id.Trim());
                }
            }

            PrimaryTimeoutMs = primaryTimeoutMs;
            FallbackTimeoutMs = fallbackTimeoutMs;
            CumulativeTimeoutMs = cumulativeTimeoutMs;
            AllowSyntheticFallbacks = allowSyntheticFallbacks;
            StopAfterStrongResolved = stopAfterStrongResolved;
        }

        public IList<string> ProviderIds
        {
            get { return providerIds.AsReadOnly(); }
        }

        public int PrimaryTimeoutMs { get; private set; }
        public int FallbackTimeoutMs { get; private set; }
        public int CumulativeTimeoutMs { get; private set; }
        public bool AllowSyntheticFallbacks { get; private set; }
        public bool StopAfterStrongResolved { get; private set; }

        /// <summary>
        /// Canonical, versioned textual form of every effective field. Two
        /// configurations produce the same string if and only if they execute
        /// identically.
        /// </summary>
        public string CanonicalForm()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("v1");
            sb.Append("|providers=").Append(string.Join(",", providerIds));
            sb.Append("|primaryMs=").Append(PrimaryTimeoutMs);
            sb.Append("|fallbackMs=").Append(FallbackTimeoutMs);
            sb.Append("|cumulativeMs=").Append(CumulativeTimeoutMs);
            sb.Append("|synthetic=").Append(AllowSyntheticFallbacks ? "1" : "0");
            sb.Append("|stopAfterStrongResolved=").Append(StopAfterStrongResolved ? "1" : "0");
            return sb.ToString();
        }

        /// <summary>Lowercase hex SHA-256 of the canonical form.</summary>
        public string Fingerprint()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(CanonicalForm());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        public static FetchExecutionPolicy FromProfile(FetchProfileDefinition profile)
        {
            if (profile == null)
                throw new ArgumentNullException("profile");

            return new FetchExecutionPolicy(
                profile.ProviderIds,
                profile.PrimaryTimeoutMs,
                profile.FallbackTimeoutMs,
                profile.CumulativeTimeoutMs,
                profile.AllowSyntheticFallbacks,
                profile.StopAfterStrongResolved);
        }

        /// <summary>
        /// Resolves the effective execution policy for a configuration. Managed
        /// profile ids resolve strictly from the catalog. The legacy Custom mode
        /// resolves the enabled provider chain from user settings and, when the
        /// benchmark override keys are present, the explicit budgets and
        /// early-stop flag recorded alongside them; without overrides the
        /// historical Custom defaults apply.
        /// </summary>
        public static FetchExecutionPolicy Resolve(Configuration config)
        {
            if (config == null)
            {
                return new FetchExecutionPolicy(
                    FetchProfileCatalog.DefaultProviderIdOrder(),
                    DefaultPrimaryTimeoutMs,
                    DefaultFallbackTimeoutMs,
                    DefaultCumulativeTimeoutMs,
                    false,
                    false);
            }

            string profileId = config.FetchProfileId;
            bool isCustom = string.Equals(profileId, "custom", StringComparison.OrdinalIgnoreCase);

            FetchProfileDefinition profile = null;
            if (!isCustom)
            {
                try { profile = FetchProfileCatalog.GetRequiredProfile(profileId); }
                catch (InvalidOperationException) { profile = null; }
            }

            if (profile != null)
                return FromProfile(profile);

            List<string> chain = ResolveCustomProviderChain(config);

            long primaryOverride = config.CustomPrimaryTimeoutMsOverride;
            long fallbackOverride = config.CustomFallbackTimeoutMsOverride;
            long cumulativeOverride = config.CustomCumulativeTimeoutMsOverride;
            int stopOverride = config.CustomStopAfterStrongResolvedOverride;

            if (primaryOverride > 0 || fallbackOverride > 0 || cumulativeOverride > 0)
            {
                int primary = primaryOverride > 0 ? (int)primaryOverride : DefaultPrimaryTimeoutMs;
                int fallback = fallbackOverride > 0 ? (int)fallbackOverride : DefaultFallbackTimeoutMs;
                int cumulative = cumulativeOverride > 0 ? (int)cumulativeOverride : DefaultCumulativeTimeoutMs;
                bool stop = stopOverride > 0;
                return new FetchExecutionPolicy(chain, primary, fallback, cumulative,
                    config.AllowSyntheticFallbacks, stop);
            }

            // Historical Custom semantics: the user timeout (seconds) caps the
            // per-provider budgets below their defaults; the cumulative ceiling
            // is fixed.
            int requested = Math.Max(5000, config.Timeout * 1000);
            int primaryBudget = Math.Min(DefaultPrimaryTimeoutMs, requested);
            int fallbackBudget = Math.Min(DefaultFallbackTimeoutMs, requested);
            bool stopDefault = stopOverride > 0;
            return new FetchExecutionPolicy(chain, primaryBudget, fallbackBudget,
                DefaultCumulativeTimeoutMs, config.AllowSyntheticFallbacks, stopDefault);
        }

        private static List<string> ResolveCustomProviderChain(Configuration config)
        {
            List<string> ordered = config.GetProviderOrderList();
            bool useThirdParty = config.UseThirdPartyFallbacks;

            List<string> chain = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string displayName in ordered)
            {
                ProviderDefinition found = FetchProfileCatalog.FindProvider(displayName);
                string id = found != null ? found.Id : displayName;
                if (!seen.Add(id))
                    continue;
                if (!config.IsProviderEnabled(displayName))
                    continue;
                if (found != null && found.IsThirdParty && !useThirdParty)
                    continue;
                chain.Add(id);
            }

            return chain;
        }
    }
}
