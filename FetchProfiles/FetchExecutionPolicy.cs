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

        // Sane bounds enforced for every policy (managed, custom, benchmark).
        internal const int MinProviderTimeoutMs = 250;
        internal const int MaxProviderTimeoutMs = 120000;
        internal const int MinCumulativeTimeoutMs = 1000;
        internal const int MaxCumulativeTimeoutMs = 300000;

        private readonly List<string> providerIds;

        /// <summary>
        /// Constructs an immutable, fully validated execution policy. Every
        /// behavior-affecting field is checked fail-closed so that a fingerprinted
        /// policy always corresponds to executable behavior: provider ids must be
        /// non-empty, unique, and resolve through the catalog, and timeouts must
        /// be positive integral values in sane ranges.
        /// </summary>
        public FetchExecutionPolicy(
            IEnumerable<string> providerIds,
            int primaryTimeoutMs,
            int fallbackTimeoutMs,
            int cumulativeTimeoutMs,
            bool allowSyntheticFallbacks,
            bool stopAfterStrongResolved,
            bool allowAndroidStoreLookup)
        {
            var validated = new List<string>();
            if (providerIds != null)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in providerIds)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        throw new ArgumentException("Execution policy contains an empty provider id.");
                    string id = raw.Trim();
                    if (!seen.Add(id))
                        throw new ArgumentException("Execution policy contains duplicate provider id '" + id + "'.");
                    if (FetchProfileCatalog.FindProvider(id) == null)
                        throw new ArgumentException("Execution policy references unknown provider id '" + id + "'.");
                    validated.Add(id);
                }
            }
            if (validated.Count == 0)
                throw new ArgumentException("Execution policy must contain at least one provider id.");

            ValidateProviderTimeout(primaryTimeoutMs, "primaryTimeoutMs");
            ValidateProviderTimeout(fallbackTimeoutMs, "fallbackTimeoutMs");
            if (cumulativeTimeoutMs < MinCumulativeTimeoutMs || cumulativeTimeoutMs > MaxCumulativeTimeoutMs)
                throw new ArgumentOutOfRangeException("cumulativeTimeoutMs", cumulativeTimeoutMs,
                    "Cumulative timeout must be between " + MinCumulativeTimeoutMs + " and " + MaxCumulativeTimeoutMs + " ms.");
            if (cumulativeTimeoutMs < primaryTimeoutMs)
                throw new ArgumentException("Cumulative timeout must be greater than or equal to the primary timeout.");

            this.providerIds = validated;
            PrimaryTimeoutMs = primaryTimeoutMs;
            FallbackTimeoutMs = fallbackTimeoutMs;
            CumulativeTimeoutMs = cumulativeTimeoutMs;
            AllowSyntheticFallbacks = allowSyntheticFallbacks;
            StopAfterStrongResolved = stopAfterStrongResolved;
            AllowAndroidStoreLookup = allowAndroidStoreLookup;
        }

        private static void ValidateProviderTimeout(int value, string paramName)
        {
            if (value < MinProviderTimeoutMs || value > MaxProviderTimeoutMs)
                throw new ArgumentOutOfRangeException(paramName, value,
                    "Provider timeout must be between " + MinProviderTimeoutMs + " and " + MaxProviderTimeoutMs + " ms.");
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
        /// Whether androidapp:// requests may perform a Google Play store lookup.
        /// Behavior-affecting and therefore part of the canonical fingerprint.
        /// </summary>
        public bool AllowAndroidStoreLookup { get; private set; }

        /// <summary>
        /// Canonical, versioned textual form of every effective field. Two
        /// configurations produce the same string if and only if they execute
        /// identically.
        /// </summary>
        public string CanonicalForm()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("v2");
            sb.Append("|providers=").Append(string.Join(",", providerIds));
            sb.Append("|primaryMs=").Append(PrimaryTimeoutMs);
            sb.Append("|fallbackMs=").Append(FallbackTimeoutMs);
            sb.Append("|cumulativeMs=").Append(CumulativeTimeoutMs);
            sb.Append("|synthetic=").Append(AllowSyntheticFallbacks ? "1" : "0");
            sb.Append("|stopAfterStrongResolved=").Append(StopAfterStrongResolved ? "1" : "0");
            sb.Append("|androidStore=").Append(AllowAndroidStoreLookup ? "1" : "0");
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
                profile.StopAfterStrongResolved,
                profile.AllowAndroidStoreLookup);
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
                    false,
                    true);
            }

            string profileId = config.FetchProfileId;
            bool isCustom = string.Equals(profileId, "custom", StringComparison.OrdinalIgnoreCase);

            if (!isCustom)
            {
                // Fail closed: an unknown managed profile id must never silently
                // degrade into Custom behavior.
                return FromProfile(FetchProfileCatalog.GetRequiredProfile(profileId));
            }

            List<string> chain = ResolveCustomProviderChain(config);

            long primaryOverride = config.CustomPrimaryTimeoutMsOverride;
            long fallbackOverride = config.CustomFallbackTimeoutMsOverride;
            long cumulativeOverride = config.CustomCumulativeTimeoutMsOverride;
            int stopOverride = config.CustomStopAfterStrongResolvedOverride;
            long androidStoreOverride = config.CustomAllowAndroidStoreLookupOverride;
            bool allowAndroidStoreLookup = androidStoreOverride >= 0
                ? androidStoreOverride > 0
                : config.UseThirdPartyFallbacks;

            if (primaryOverride > 0 || fallbackOverride > 0 || cumulativeOverride > 0)
            {
                int primary = primaryOverride > 0 ? (int)primaryOverride : DefaultPrimaryTimeoutMs;
                int fallback = fallbackOverride > 0 ? (int)fallbackOverride : DefaultFallbackTimeoutMs;
                int cumulative = cumulativeOverride > 0 ? (int)cumulativeOverride : DefaultCumulativeTimeoutMs;
                bool stop = stopOverride > 0;
                return new FetchExecutionPolicy(chain, primary, fallback, cumulative,
                    config.AllowSyntheticFallbacks, stop, allowAndroidStoreLookup);
            }

            // Historical Custom semantics: the user timeout (seconds) caps the
            // per-provider budgets below their defaults; the cumulative ceiling
            // is fixed.
            int requested = Math.Max(5000, config.Timeout * 1000);
            int primaryBudget = Math.Min(DefaultPrimaryTimeoutMs, requested);
            int fallbackBudget = Math.Min(DefaultFallbackTimeoutMs, requested);
            bool stopDefault = stopOverride > 0;
            return new FetchExecutionPolicy(chain, primaryBudget, fallbackBudget,
                DefaultCumulativeTimeoutMs, config.AllowSyntheticFallbacks, stopDefault,
                allowAndroidStoreLookup);
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
