using System;
using System.Collections.Generic;

namespace KeeFetch.FetchProfiles
{
    /// <summary>
    /// User-facing wording for fetch profiles. The generated catalog carries
    /// study terminology; dialogs use these plain-language strings instead so
    /// every surface describes a profile, and what it contacts, the same way.
    /// </summary>
    internal static class ProfilePresentation
    {
        internal const string CustomProfileId = "custom";

        /// <summary>Display name without decoration, e.g. "Balanced" or "Custom".</summary>
        internal static string DisplayName(string profileId)
        {
            FetchProfileDefinition profile = Find(profileId);
            if (profile != null)
                return profile.DisplayName;
            return "Custom";
        }

        /// <summary>Display name for choice lists, marking the default profile.</summary>
        internal static string ChoiceName(FetchProfileDefinition profile)
        {
            if (profile == null)
                throw new ArgumentNullException("profile");
            return IsDefault(profile.Id) ? profile.DisplayName + " (recommended)" : profile.DisplayName;
        }

        internal static bool IsDefault(string profileId)
        {
            return string.Equals(profileId, LegacyProfileMigration.DefaultProfileId,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>One sentence on what the profile is for.</summary>
        internal static string Summary(string profileId)
        {
            if (string.Equals(profileId, "everyday", StringComparison.OrdinalIgnoreCase))
                return "Finds an icon for most entries. Asks each website first and uses icon services only when the site has no usable icon.";
            if (string.Equals(profileId, "privacy", StringComparison.OrdinalIgnoreCase))
                return "Asks only each entry's own website. No icon services are used, so some entries will stay without an icon.";
            if (IsCustom(profileId))
                return "Uses the providers, order and timeouts you choose on the Providers and Advanced tabs.";

            FetchProfileDefinition profile = Find(profileId);
            return profile != null ? profile.IntendedUse + "." : string.Empty;
        }

        /// <summary>Who receives requests under this profile, as one sentence.</summary>
        internal static string ContactsLine(string profileId)
        {
            if (IsCustom(profileId))
                return "Contacts: the providers you enable on the Providers tab.";

            FetchProfileDefinition profile = Find(profileId);
            if (profile == null)
                return string.Empty;

            return ContactsLine(profile.ProviderIds, profile.AllowAndroidStoreLookup);
        }

        internal static string ContactsLine(IList<string> providerIds, bool allowAndroidStoreLookup)
        {
            List<string> services = new List<string>();
            bool direct = false;
            if (providerIds != null)
            {
                foreach (string id in providerIds)
                {
                    ProviderDefinition provider = FetchProfileCatalog.FindProvider(id);
                    if (provider == null)
                        continue;
                    if (provider.Id.Equals("direct-site", StringComparison.OrdinalIgnoreCase))
                        direct = true;
                    else
                        services.Add(provider.DisplayName);
                }
            }

            string text;
            if (services.Count == 0)
                text = direct ? "Contacts: only the entry's own website" : "Contacts: no one";
            else if (direct)
                text = "Contacts: the entry's own website, then " + JoinNames(services);
            else
                text = "Contacts: " + JoinNames(services);

            if (allowAndroidStoreLookup)
                text += "; Google Play for Android app entries";

            return text + ".";
        }

        /// <summary>The third-party services a profile may contact, e.g. "Google and Twenty Icons".</summary>
        internal static string ThirdPartyNames(string profileId)
        {
            FetchProfileDefinition profile = Find(profileId);
            if (profile == null)
                return string.Empty;

            List<string> services = new List<string>();
            foreach (string id in profile.ProviderIds)
            {
                ProviderDefinition provider = FetchProfileCatalog.FindProvider(id);
                if (provider != null && provider.IsThirdParty)
                    services.Add(provider.DisplayName);
            }

            return JoinNames(services);
        }

        internal static string JoinNames(IList<string> names)
        {
            if (names == null || names.Count == 0)
                return string.Empty;
            if (names.Count == 1)
                return names[0];

            List<string> head = new List<string>();
            for (int i = 0; i < names.Count - 1; i++)
                head.Add(names[i]);
            return string.Join(", ", head.ToArray()) + " and " + names[names.Count - 1];
        }

        private static bool IsCustom(string profileId)
        {
            return string.IsNullOrWhiteSpace(profileId) ||
                profileId.Trim().Equals(CustomProfileId, StringComparison.OrdinalIgnoreCase);
        }

        private static FetchProfileDefinition Find(string profileId)
        {
            if (IsCustom(profileId))
                return null;

            try
            {
                return FetchProfileCatalog.GetRequiredProfile(profileId);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
