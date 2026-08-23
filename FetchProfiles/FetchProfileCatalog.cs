using System;
using System.Collections.Generic;
using System.Linq;

namespace KeeFetch.FetchProfiles
{
    internal static partial class FetchProfileCatalog
    {
        private static readonly List<ProviderDefinition> providers = new List<ProviderDefinition>
        {
            new ProviderDefinition("direct-site", "Direct Site", false, false, false),
            new ProviderDefinition("twenty-icons", "Twenty Icons", true, false, false),
            new ProviderDefinition("duckduckgo", "DuckDuckGo", true, false, false),
            new ProviderDefinition("google", "Google", true, false, false),
            new ProviderDefinition("yandex", "Yandex", true, false, false),
            new ProviderDefinition("favicone", "Favicone", true, true, true),
            new ProviderDefinition("icon-horse", "Icon Horse", true, true, true)
        };

        private static List<FetchProfileDefinition> managedProfiles;
        private static readonly object managedLock = new object();

        public static IList<ProviderDefinition> Providers
        {
            get { return providers.AsReadOnly(); }
        }

        public static ProviderDefinition FindProvider(string idOrDisplayName)
        {
            if (string.IsNullOrWhiteSpace(idOrDisplayName))
                return null;

            string trimmed = idOrDisplayName.Trim();
            for (int i = 0; i < providers.Count; i++)
            {
                ProviderDefinition p = providers[i];
                if (trimmed.Equals(p.Id, StringComparison.OrdinalIgnoreCase))
                    return p;
                if (trimmed.Equals(p.DisplayName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            return null;
        }

        public static List<string> DefaultProviderDisplayOrder
        {
            get { return Providers.Select(p => p.DisplayName).ToList(); }
        }

        public static List<string> DefaultProviderIdOrder()
        {
            return Providers.Select(p => p.Id).ToList();
        }

        public static List<string> NormalizeProviderOrder(IEnumerable<string> raw)
        {
            List<string> ordered = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (raw != null)
            {
                foreach (string entry in raw)
                {
                    if (entry == null)
                        continue;
                    string trimmed = entry.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    ProviderDefinition found = FindProvider(trimmed);
                    string canonical = found != null ? found.DisplayName : trimmed;

                    if (seen.Add(canonical))
                        ordered.Add(canonical);
                }
            }

            foreach (ProviderDefinition provider in Providers)
            {
                if (seen.Add(provider.DisplayName))
                    ordered.Add(provider.DisplayName);
            }

            return ordered;
        }

        public static IList<FetchProfileDefinition> ManagedProfiles
        {
            get
            {
                if (managedProfiles == null)
                {
                    lock (managedLock)
                    {
                        if (managedProfiles == null)
                        {
                            List<FetchProfileDefinition> created = CreateManagedProfiles();
                            ValidateManagedProfiles(created);
                            managedProfiles = created;
                        }
                    }
                }

                return managedProfiles.AsReadOnly();
            }
        }

        public static FetchProfileDefinition GetRequiredProfile(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Profile id must not be empty.");

            string trimmed = id.Trim();
            IList<FetchProfileDefinition> profiles = ManagedProfiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (trimmed.Equals(profiles[i].Id, StringComparison.OrdinalIgnoreCase))
                    return profiles[i];
            }

            throw new InvalidOperationException("Unknown profile id: " + id);
        }

        private static void ValidateManagedProfiles(List<FetchProfileDefinition> profiles)
        {
            if (profiles == null)
                throw new InvalidOperationException("Managed profiles must not be null.");

            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < profiles.Count; i++)
            {
                FetchProfileDefinition p = profiles[i];
                if (p == null)
                    throw new InvalidOperationException("Profile at index " + i + " is null.");

                if (string.IsNullOrWhiteSpace(p.Id))
                    throw new InvalidOperationException("Profile id must not be empty.");

                if (!seenIds.Add(p.Id))
                    throw new InvalidOperationException("Duplicate profile id: " + p.Id);

                if (string.IsNullOrWhiteSpace(p.DisplayName))
                    throw new InvalidOperationException("Profile display name must not be empty for id: " + p.Id);

                if (string.IsNullOrWhiteSpace(p.Description))
                    throw new InvalidOperationException("Profile description must not be empty for id: " + p.Id);

                if (string.IsNullOrWhiteSpace(p.IntendedUse))
                    throw new InvalidOperationException("Profile intended use must not be empty for id: " + p.Id);

                if (string.IsNullOrWhiteSpace(p.EvidenceReport))
                    throw new InvalidOperationException("Profile evidence report must not be empty for id: " + p.Id);

                if (p.IsVisible && p.ProviderIds.Count == 0)
                    throw new InvalidOperationException("Visible profile must have at least one provider: " + p.Id);

                if (p.CumulativeTimeoutMs < p.PrimaryTimeoutMs)
                    throw new InvalidOperationException("Cumulative timeout must be >= primary timeout for profile: " + p.Id);

                if (p.PrimaryTimeoutMs <= 0 || p.FallbackTimeoutMs <= 0 || p.CumulativeTimeoutMs <= 0)
                    throw new InvalidOperationException("Timeout values must be positive for profile: " + p.Id);

                HashSet<string> seenProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < p.ProviderIds.Count; j++)
                {
                    string providerId = p.ProviderIds[j];
                    if (string.IsNullOrWhiteSpace(providerId))
                        throw new InvalidOperationException("Provider id must not be empty in profile: " + p.Id);

                    if (!seenProviders.Add(providerId))
                        throw new InvalidOperationException("Duplicate provider '" + providerId + "' in profile: " + p.Id);

                    ProviderDefinition provider = FindProvider(providerId);
                    if (provider == null)
                        throw new InvalidOperationException("Unknown provider '" + providerId + "' in profile: " + p.Id);
                }

                if (p.Id.Equals("privacy", StringComparison.OrdinalIgnoreCase))
                {
                    for (int j = 0; j < p.ProviderIds.Count; j++)
                    {
                        ProviderDefinition provider = FindProvider(p.ProviderIds[j]);
                        if (provider != null && provider.IsThirdParty)
                            throw new InvalidOperationException("Privacy profile must not contain third-party provider: " + provider.Id);
                    }

                    if (p.AllowAndroidStoreLookup)
                        throw new InvalidOperationException("Privacy profile must not allow Android store lookups: " + p.Id);
                }
            }
        }
    }
}
