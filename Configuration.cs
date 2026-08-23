using System;
using System.Collections.Generic;
using System.Linq;
using KeeFetch.FetchProfiles;
using KeePass.App.Configuration;

namespace KeeFetch
{
    /// <summary>
    /// Manages KeeFetch plugin configuration settings stored in KeePass custom config.
    /// </summary>
    public sealed class Configuration
    {
        private const string Prefix = "KeeFetch.";
        private readonly AceCustomConfig config;

        private FetchPresetMode? fetchPresetMode;
        private string fetchProfileId;
        private int? profileSchemaVersion;
        private bool? prefixUrls;
        private bool? useTitleField;
        private bool? skipExistingIcons;
        private bool? autoSave;
        private bool? allowSelfSignedCerts;
        private bool? useThirdPartyFallbacks;
        private bool? allowSyntheticFallbacks;
        private bool? hasSeenFirstRunDisclosure;

        private bool? enableDirectSiteProvider;
        private bool? enableTwentyIconsProvider;
        private bool? enableDuckDuckGoProvider;
        private bool? enableGoogleProvider;
        private bool? enableYandexProvider;
        private bool? enableFaviconeProvider;
        private bool? enableIconHorseProvider;

        private int? maxIconSize;
        private int? timeout;

        private string iconNamePrefix;
        private string providerOrder;

        public Configuration(AceCustomConfig customConfig)
        {
            config = customConfig;
        }

        public bool PrefixUrls
        {
            get
            {
                if (!prefixUrls.HasValue)
                    prefixUrls = config.GetBool(Prefix + "PrefixUrls", true);
                return prefixUrls.Value;
            }
            set
            {
                prefixUrls = value;
                config.SetBool(Prefix + "PrefixUrls", value);
            }
        }

        public string FetchProfileId
        {
            get
            {
                if (fetchProfileId != null)
                    return fetchProfileId;

                string stored = config.GetString(Prefix + "FetchProfileId", null);
                if (stored != null)
                {
                    string trimmed = stored.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || !IsKnownProfileId(trimmed))
                    {
                        fetchProfileId = "custom";
                        return fetchProfileId;
                    }

                    string canonical = GetCanonicalProfileId(trimmed);
                    fetchProfileId = canonical;
                    return fetchProfileId;
                }

                string legacyRaw = config.GetString(Prefix + "FetchPresetMode", null);
                bool isNewInstall = legacyRaw == null;
                string mapped = LegacyProfileMigration.MapLegacyValue(legacyRaw, isNewInstall);
                fetchProfileId = GetCanonicalProfileId(mapped);
                config.SetString(Prefix + "FetchProfileId", fetchProfileId);
                config.SetLong(Prefix + "ProfileSchemaVersion", LegacyProfileMigration.CurrentSchemaVersion);
                profileSchemaVersion = LegacyProfileMigration.CurrentSchemaVersion;
                fetchPresetMode = MapProfileIdToPresetMode(fetchProfileId);
                return fetchProfileId;
            }
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "custom" : value.Trim();
                string canonical;
                if (IsKnownProfileId(normalized))
                    canonical = GetCanonicalProfileId(normalized);
                else
                    canonical = "custom";

                fetchProfileId = canonical;
                fetchPresetMode = MapProfileIdToPresetMode(canonical);
                config.SetString(Prefix + "FetchProfileId", canonical);
                config.SetLong(Prefix + "ProfileSchemaVersion", LegacyProfileMigration.CurrentSchemaVersion);
                profileSchemaVersion = LegacyProfileMigration.CurrentSchemaVersion;
            }
        }

        public int ProfileSchemaVersion
        {
            get
            {
                if (profileSchemaVersion.HasValue)
                    return profileSchemaVersion.Value;

                long v = config.GetLong(Prefix + "ProfileSchemaVersion", 0);
                profileSchemaVersion = (int)v;
                return profileSchemaVersion.Value;
            }
            set
            {
                profileSchemaVersion = value;
                config.SetLong(Prefix + "ProfileSchemaVersion", value);
            }
        }

        public FetchPresetMode FetchPresetMode
        {
            get
            {
                if (!fetchPresetMode.HasValue)
                {
                    string pid = FetchProfileId;
                    fetchPresetMode = MapProfileIdToPresetMode(pid);
                }
                return fetchPresetMode.Value;
            }
            set
            {
                fetchPresetMode = value;
                string profileId = MapPresetModeToProfileId(value);
                FetchProfileId = profileId;
                config.SetString(Prefix + "FetchPresetMode", value.ToString());
            }
        }

        public bool UseTitleField
        {
            get
            {
                if (!useTitleField.HasValue)
                    useTitleField = config.GetBool(Prefix + "UseTitleField", true);
                return useTitleField.Value;
            }
            set
            {
                useTitleField = value;
                config.SetBool(Prefix + "UseTitleField", value);
            }
        }

        public bool SkipExistingIcons
        {
            get
            {
                if (!skipExistingIcons.HasValue)
                    skipExistingIcons = config.GetBool(Prefix + "SkipExistingIcons", false);
                return skipExistingIcons.Value;
            }
            set
            {
                skipExistingIcons = value;
                config.SetBool(Prefix + "SkipExistingIcons", value);
            }
        }

        public bool AutoSave
        {
            get
            {
                if (!autoSave.HasValue)
                    autoSave = config.GetBool(Prefix + "AutoSave", false);
                return autoSave.Value;
            }
            set
            {
                autoSave = value;
                config.SetBool(Prefix + "AutoSave", value);
            }
        }

        public bool AllowSelfSignedCerts
        {
            get
            {
                if (!allowSelfSignedCerts.HasValue)
                    allowSelfSignedCerts = config.GetBool(Prefix + "AllowSelfSignedCerts", false);
                return allowSelfSignedCerts.Value;
            }
            set
            {
                allowSelfSignedCerts = value;
                config.SetBool(Prefix + "AllowSelfSignedCerts", value);
            }
        }

        public bool UseThirdPartyFallbacks
        {
            get
            {
                if (!useThirdPartyFallbacks.HasValue)
                    useThirdPartyFallbacks = config.GetBool(Prefix + "UseThirdPartyFallbacks", true);
                return useThirdPartyFallbacks.Value;
            }
            set
            {
                useThirdPartyFallbacks = value;
                config.SetBool(Prefix + "UseThirdPartyFallbacks", value);
            }
        }

        public bool AllowSyntheticFallbacks
        {
            get
            {
                if (!allowSyntheticFallbacks.HasValue)
                    allowSyntheticFallbacks = config.GetBool(Prefix + "AllowSyntheticFallbacks", true);
                return allowSyntheticFallbacks.Value;
            }
            set
            {
                allowSyntheticFallbacks = value;
                config.SetBool(Prefix + "AllowSyntheticFallbacks", value);
            }
        }

        public bool HasSeenFirstRunDisclosure
        {
            get
            {
                if (!hasSeenFirstRunDisclosure.HasValue)
                    hasSeenFirstRunDisclosure = config.GetBool(Prefix + "HasSeenFirstRunDisclosure", false);
                return hasSeenFirstRunDisclosure.Value;
            }
            set
            {
                hasSeenFirstRunDisclosure = value;
                config.SetBool(Prefix + "HasSeenFirstRunDisclosure", value);
            }
        }

        public int MaxIconSize
        {
            get
            {
                if (!maxIconSize.HasValue)
                    maxIconSize = (int)config.GetLong(Prefix + "MaxIconSize", 128);
                return maxIconSize.Value;
            }
            set
            {
                int clamped = Math.Max(16, Math.Min(256, value));
                maxIconSize = clamped;
                config.SetLong(Prefix + "MaxIconSize", clamped);
            }
        }

        public int Timeout
        {
            get
            {
                if (!timeout.HasValue)
                    timeout = (int)config.GetLong(Prefix + "Timeout", 15);
                return timeout.Value;
            }
            set
            {
                int clamped = Math.Max(5, Math.Min(60, value));
                timeout = clamped;
                config.SetLong(Prefix + "Timeout", clamped);
            }
        }

        public string IconNamePrefix
        {
            get
            {
                if (iconNamePrefix == null)
                    iconNamePrefix = config.GetString(Prefix + "IconNamePrefix", "keefetch-");
                return iconNamePrefix;
            }
            set
            {
                iconNamePrefix = value ?? string.Empty;
                config.SetString(Prefix + "IconNamePrefix", iconNamePrefix);
            }
        }

        public string ProviderOrder
        {
            get
            {
                if (providerOrder == null)
                {
                    providerOrder = config.GetString(Prefix + "ProviderOrder",
                        string.Join(",", FetchProfileCatalog.DefaultProviderDisplayOrder));
                }
                return providerOrder;
            }
            set
            {
                providerOrder = string.IsNullOrWhiteSpace(value)
                    ? string.Join(",", FetchProfileCatalog.DefaultProviderDisplayOrder)
                    : value;
                config.SetString(Prefix + "ProviderOrder", providerOrder);
            }
        }

        public bool EnableDirectSiteProvider
        {
            get
            {
                if (!enableDirectSiteProvider.HasValue)
                    enableDirectSiteProvider = config.GetBool(Prefix + "EnableDirectSiteProvider", true);
                return enableDirectSiteProvider.Value;
            }
            set
            {
                enableDirectSiteProvider = value;
                config.SetBool(Prefix + "EnableDirectSiteProvider", value);
            }
        }

        public bool EnableTwentyIconsProvider
        {
            get
            {
                if (!enableTwentyIconsProvider.HasValue)
                    enableTwentyIconsProvider = config.GetBool(Prefix + "EnableTwentyIconsProvider", true);
                return enableTwentyIconsProvider.Value;
            }
            set
            {
                enableTwentyIconsProvider = value;
                config.SetBool(Prefix + "EnableTwentyIconsProvider", value);
            }
        }

        public bool EnableDuckDuckGoProvider
        {
            get
            {
                if (!enableDuckDuckGoProvider.HasValue)
                    enableDuckDuckGoProvider = config.GetBool(Prefix + "EnableDuckDuckGoProvider", true);
                return enableDuckDuckGoProvider.Value;
            }
            set
            {
                enableDuckDuckGoProvider = value;
                config.SetBool(Prefix + "EnableDuckDuckGoProvider", value);
            }
        }

        public bool EnableGoogleProvider
        {
            get
            {
                if (!enableGoogleProvider.HasValue)
                    enableGoogleProvider = config.GetBool(Prefix + "EnableGoogleProvider", true);
                return enableGoogleProvider.Value;
            }
            set
            {
                enableGoogleProvider = value;
                config.SetBool(Prefix + "EnableGoogleProvider", value);
            }
        }

        public bool EnableYandexProvider
        {
            get
            {
                if (!enableYandexProvider.HasValue)
                    enableYandexProvider = config.GetBool(Prefix + "EnableYandexProvider", true);
                return enableYandexProvider.Value;
            }
            set
            {
                enableYandexProvider = value;
                config.SetBool(Prefix + "EnableYandexProvider", value);
            }
        }

        public bool EnableFaviconeProvider
        {
            get
            {
                if (!enableFaviconeProvider.HasValue)
                    enableFaviconeProvider = config.GetBool(Prefix + "EnableFaviconeProvider", true);
                return enableFaviconeProvider.Value;
            }
            set
            {
                enableFaviconeProvider = value;
                config.SetBool(Prefix + "EnableFaviconeProvider", value);
            }
        }

        public bool EnableIconHorseProvider
        {
            get
            {
                if (!enableIconHorseProvider.HasValue)
                    enableIconHorseProvider = config.GetBool(Prefix + "EnableIconHorseProvider", true);
                return enableIconHorseProvider.Value;
            }
            set
            {
                enableIconHorseProvider = value;
                config.SetBool(Prefix + "EnableIconHorseProvider", value);
            }
        }

        public bool IsProviderEnabled(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return false;

            string normalized = NormalizeProviderName(providerName);
            switch (normalized)
            {
                case "Direct Site":
                    return EnableDirectSiteProvider;
                case "Twenty Icons":
                    return EnableTwentyIconsProvider;
                case "DuckDuckGo":
                    return EnableDuckDuckGoProvider;
                case "Google":
                    return EnableGoogleProvider;
                case "Yandex":
                    return EnableYandexProvider;
                case "Favicone":
                    return EnableFaviconeProvider;
                case "Icon Horse":
                    return EnableIconHorseProvider;
                default:
                    return true;
            }
        }

        public void SetProviderEnabled(string providerName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return;

            string normalized = NormalizeProviderName(providerName);
            switch (normalized)
            {
                case "Direct Site":
                    EnableDirectSiteProvider = enabled;
                    break;
                case "Twenty Icons":
                    EnableTwentyIconsProvider = enabled;
                    break;
                case "DuckDuckGo":
                    EnableDuckDuckGoProvider = enabled;
                    break;
                case "Google":
                    EnableGoogleProvider = enabled;
                    break;
                case "Yandex":
                    EnableYandexProvider = enabled;
                    break;
                case "Favicone":
                    EnableFaviconeProvider = enabled;
                    break;
                case "Icon Horse":
                    EnableIconHorseProvider = enabled;
                    break;
            }
        }

        private static string NormalizeProviderName(string providerName)
        {
            if (providerName == null)
                return string.Empty;

            string trimmed = providerName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            var found = FetchProfileCatalog.FindProvider(trimmed);
            return found != null ? found.DisplayName : trimmed;
        }

        public List<string> GetProviderOrderList()
        {
            if (string.IsNullOrWhiteSpace(ProviderOrder))
                return FetchProfileCatalog.DefaultProviderDisplayOrder;

            string[] parts = ProviderOrder.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return FetchProfileCatalog.NormalizeProviderOrder(parts);
        }

        public bool ShouldStopAfterStrongResolvedProvider()
        {
            string pid = FetchProfileId;
            if (pid.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return CustomStopAfterStrongResolvedOverride > 0;
            try { return FetchProfileCatalog.GetRequiredProfile(pid).StopAfterStrongResolved; }
            catch (InvalidOperationException) { return false; }
        }

        // Benchmark override keys: when present in custom mode they pin the
        // exact execution policy a benchmark candidate runs with (see
        // FetchExecutionPolicy.Resolve). Real user configs never set them.
        internal long CustomPrimaryTimeoutMsOverride
        {
            get { return config.GetLong(Prefix + "CustomPrimaryTimeoutMs", 0); }
        }

        internal long CustomFallbackTimeoutMsOverride
        {
            get { return config.GetLong(Prefix + "CustomFallbackTimeoutMs", 0); }
        }

        internal long CustomCumulativeTimeoutMsOverride
        {
            get { return config.GetLong(Prefix + "CustomCumulativeTimeoutMs", 0); }
        }

        internal int CustomStopAfterStrongResolvedOverride
        {
            get { return (int)config.GetLong(Prefix + "CustomStopAfterStrongResolved", -1); }
        }

        /// <summary>
        /// Benchmark override for the Android store (Google Play) lookup permission
        /// of a custom execution policy: negative = derive from
        /// UseThirdPartyFallbacks, 0 = deny, positive = allow. Participates in the
        /// policy fingerprint.
        /// </summary>
        internal long CustomAllowAndroidStoreLookupOverride
        {
            get { return config.GetLong(Prefix + "CustomAllowAndroidStoreLookup", -1); }
        }

        private static bool TryGetProfileForMode(FetchPresetMode mode, out FetchProfileDefinition profile)
        {
            profile = null;
            string id = MapPresetModeToProfileId(mode);
            if (id.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return false;
            try { profile = FetchProfileCatalog.GetRequiredProfile(id); return true; }
            catch (InvalidOperationException) { return false; }
        }

        private static bool TryGetProfileForId(string profileId, out FetchProfileDefinition profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(profileId))
                return false;
            string canonical = GetCanonicalProfileId(profileId);
            try { profile = FetchProfileCatalog.GetRequiredProfile(canonical); return true; }
            catch (InvalidOperationException) { return false; }
        }

        public static string GetPresetDescription(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return profile.Description;
            return "Manual configuration. KeeFetch will use the exact provider toggles and timeout values shown below.";
        }

        public static int GetPresetTimeout(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return Math.Max(5, (profile.PrimaryTimeoutMs + 999) / 1000);
            return 15;
        }

        public static bool GetPresetUseThirdPartyFallbacks(FetchPresetMode mode)
        {
            if (mode == FetchPresetMode.Custom) return false;
            return true;
        }

        public static bool GetPresetAllowSyntheticFallbacks(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return profile.AllowSyntheticFallbacks;
            return mode == FetchPresetMode.Balanced || mode == FetchPresetMode.Thorough;
        }

        public static List<string> GetPresetProviderOrderList(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
            {
                List<string> list = new List<string>();
                foreach (string pid in profile.ProviderIds)
                {
                    ProviderDefinition found = FetchProfileCatalog.FindProvider(pid);
                    list.Add(found != null ? found.DisplayName : pid);
                }
                return list;
            }
            return new List<string>(FetchProfileCatalog.DefaultProviderDisplayOrder);
        }

        public static int GetPresetMaxCumulativeTimeoutMs(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return profile.CumulativeTimeoutMs;
            return 45000;
        }

        public static int GetPresetPrimaryProviderTimeoutMs(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return profile.PrimaryTimeoutMs;
            return 10000;
        }

        public static int GetPresetFallbackProviderTimeoutMs(FetchPresetMode mode)
        {
            FetchProfileDefinition profile;
            if (TryGetProfileForMode(mode, out profile))
                return profile.FallbackTimeoutMs;
            return 5000;
        }

        public static bool IsProviderEnabledByPreset(FetchPresetMode mode, string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return false;

            FetchProfileDefinition profile;
            if (!TryGetProfileForMode(mode, out profile))
                return true;

            string normalized = NormalizeProviderName(providerName);
            foreach (string pid in profile.ProviderIds)
            {
                ProviderDefinition found = FetchProfileCatalog.FindProvider(pid);
                string name = found != null ? found.DisplayName : pid;
                if (normalized.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static FetchPresetMode ParseFetchPresetMode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return FetchPresetMode.Custom;

            string normalized = raw.Trim();
            if (normalized.Equals(FetchPresetMode.Fast.ToString(), StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Fast;
            if (normalized.Equals(FetchPresetMode.Balanced.ToString(), StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Balanced;
            if (normalized.Equals(FetchPresetMode.Thorough.ToString(), StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Thorough;
            return FetchPresetMode.Custom;
        }

        private static bool IsKnownProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            string trimmed = id.Trim();
            if (trimmed.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Equals("bulk-fast", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Equals("everyday", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Equals("privacy", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Equals("max-coverage", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static string GetCanonicalProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "custom";
            string trimmed = id.Trim();
            if (trimmed.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return "custom";
            if (trimmed.Equals("bulk-fast", StringComparison.OrdinalIgnoreCase))
                return "bulk-fast";
            if (trimmed.Equals("everyday", StringComparison.OrdinalIgnoreCase))
                return "everyday";
            if (trimmed.Equals("privacy", StringComparison.OrdinalIgnoreCase))
                return "privacy";
            if (trimmed.Equals("max-coverage", StringComparison.OrdinalIgnoreCase))
                return "max-coverage";
            return "custom";
        }

        private static FetchPresetMode MapProfileIdToPresetMode(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return FetchPresetMode.Custom;
            string trimmed = profileId.Trim();
            if (trimmed.Equals("bulk-fast", StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Fast;
            if (trimmed.Equals("everyday", StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Balanced;
            if (trimmed.Equals("max-coverage", StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Thorough;
            if (trimmed.Equals("privacy", StringComparison.OrdinalIgnoreCase))
                return FetchPresetMode.Custom;
            return FetchPresetMode.Custom;
        }

        private static string MapPresetModeToProfileId(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return "bulk-fast";
                case FetchPresetMode.Balanced:
                    return "everyday";
                case FetchPresetMode.Thorough:
                    return "max-coverage";
                default:
                    return "custom";
            }
        }
    }

    public enum FetchPresetMode
    {
        Custom = 0,
        Fast = 1,
        Balanced = 2,
        Thorough = 3
    }
}
