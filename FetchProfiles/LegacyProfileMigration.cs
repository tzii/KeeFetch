using System;

namespace KeeFetch.FetchProfiles
{
    internal static class LegacyProfileMigration
    {
        internal const int CurrentSchemaVersion = 1;

        internal static string MapLegacyValue(string raw, bool isNewInstall)
        {
            if (string.IsNullOrWhiteSpace(raw)) return isNewInstall ? "everyday" : "custom";
            string v = raw.Trim();
            if (v.Equals("Fast", StringComparison.OrdinalIgnoreCase)) return "bulk-fast";
            if (v.Equals("Balanced", StringComparison.OrdinalIgnoreCase)) return "everyday";
            if (v.Equals("Thorough", StringComparison.OrdinalIgnoreCase)) return "max-coverage";
            if (v.Equals("Custom", StringComparison.OrdinalIgnoreCase)) return "custom";
            return "custom";
        }
    }
}
