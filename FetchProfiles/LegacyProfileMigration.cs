using System;

namespace KeeFetch.FetchProfiles
{
    internal static class LegacyProfileMigration
    {
        /// <summary>
        /// 1: v1.3 profile ids. 2: v1.4 retires the Fast and Precise profiles;
        /// a stored retired id moves to Balanced and the first-run choice is
        /// shown again, because Balanced contacts a different provider set.
        /// </summary>
        internal const int CurrentSchemaVersion = 2;

        internal const string DefaultProfileId = "everyday";

        internal static string MapLegacyValue(string raw, bool isNewInstall)
        {
            if (string.IsNullOrWhiteSpace(raw)) return isNewInstall ? DefaultProfileId : "custom";
            string v = raw.Trim();
            if (v.Equals("Fast", StringComparison.OrdinalIgnoreCase)) return "bulk-fast";
            if (v.Equals("Balanced", StringComparison.OrdinalIgnoreCase)) return "everyday";
            if (v.Equals("Thorough", StringComparison.OrdinalIgnoreCase)) return "max-coverage";
            if (v.Equals("Custom", StringComparison.OrdinalIgnoreCase)) return "custom";
            return "custom";
        }

        /// <summary>
        /// True when <paramref name="profileId"/> names a managed profile that is
        /// no longer offered to users (hidden in the catalog).
        /// </summary>
        internal static bool IsRetiredProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            string trimmed = profileId.Trim();
            if (trimmed.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles)
            {
                if (trimmed.Equals(profile.Id, StringComparison.OrdinalIgnoreCase))
                    return !profile.IsVisible;
            }

            return false;
        }
    }
}
