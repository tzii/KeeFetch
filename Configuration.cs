using System;
using System.Collections.Generic;
using System.Linq;
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

        public FetchPresetMode FetchPresetMode
        {
            get
            {
                if (!fetchPresetMode.HasValue)
                {
                    fetchPresetMode = ParseFetchPresetMode(config.GetString(
                        Prefix + "FetchPresetMode", FetchPresetMode.Custom.ToString()));
                }
                return fetchPresetMode.Value;
            }
            set
            {
                fetchPresetMode = value;
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
                        string.Join(",", FaviconDownloader.DefaultProviderOrder));
                }
                return providerOrder;
            }
            set
            {
                providerOrder = string.IsNullOrWhiteSpace(value)
                    ? string.Join(",", FaviconDownloader.DefaultProviderOrder)
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

            string value = providerName.Trim();
            if (value.Equals("direct site", StringComparison.OrdinalIgnoreCase)) return "Direct Site";
            if (value.Equals("twenty icons", StringComparison.OrdinalIgnoreCase)) return "Twenty Icons";
            if (value.Equals("duckduckgo", StringComparison.OrdinalIgnoreCase)) return "DuckDuckGo";
            if (value.Equals("google", StringComparison.OrdinalIgnoreCase)) return "Google";
            if (value.Equals("yandex", StringComparison.OrdinalIgnoreCase)) return "Yandex";
            if (value.Equals("favicone", StringComparison.OrdinalIgnoreCase)) return "Favicone";
            if (value.Equals("icon horse", StringComparison.OrdinalIgnoreCase)) return "Icon Horse";
            return value;
        }

        public List<string> GetProviderOrderList()
        {
            var configured = new List<string>();
            if (!string.IsNullOrWhiteSpace(ProviderOrder))
            {
                configured.AddRange(ProviderOrder
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizeProviderName)
                    .Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            if (configured.Count == 0)
                configured.AddRange(FaviconDownloader.DefaultProviderOrder);

            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string provider in configured)
            {
                if (string.IsNullOrWhiteSpace(provider))
                    continue;

                if (seen.Add(provider))
                    ordered.Add(provider);
            }

            foreach (string provider in FaviconDownloader.DefaultProviderOrder)
            {
                if (seen.Add(provider))
                    ordered.Add(provider);
            }

            return ordered;
        }

        public bool ShouldStopAfterStrongResolvedProvider()
        {
            return FetchPresetMode == FetchPresetMode.Fast ||
                   FetchPresetMode == FetchPresetMode.Balanced;
        }

        public static string GetPresetDescription(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return "Shortest path. Tries direct site, then a compact strong-resolver chain with reduced time budgets for faster large batches.";
                case FetchPresetMode.Balanced:
                    return "Recommended default. Uses direct site, Google, and a lightweight synthetic fallback to balance coverage and batch speed.";
                case FetchPresetMode.Thorough:
                    return "Availability-first mode. Uses the full resolver chain with the largest time budgets and synthetic fallbacks for maximum coverage.";
                default:
                    return "Manual configuration. KeeFetch will use the exact provider toggles and timeout values shown below.";
            }
        }

        public static int GetPresetTimeout(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return 5;
                case FetchPresetMode.Balanced:
                    return 7;
                case FetchPresetMode.Thorough:
                    return 15;
                default:
                    return 15;
            }
        }

        public static bool GetPresetUseThirdPartyFallbacks(FetchPresetMode mode)
        {
            return mode != FetchPresetMode.Custom;
        }

        public static bool GetPresetAllowSyntheticFallbacks(FetchPresetMode mode)
        {
            return mode == FetchPresetMode.Balanced ||
                   mode == FetchPresetMode.Thorough;
        }

        public static List<string> GetPresetProviderOrderList(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return new List<string>
                    {
                        "Direct Site",
                        "Google",
                        "Twenty Icons"
                    };
                case FetchPresetMode.Balanced:
                    return new List<string>
                    {
                        "Direct Site",
                        "Google",
                        "Favicone"
                    };
                case FetchPresetMode.Thorough:
                    return new List<string>(FaviconDownloader.DefaultProviderOrder);
                default:
                    return new List<string>(FaviconDownloader.DefaultProviderOrder);
            }
        }

        public static int GetPresetMaxCumulativeTimeoutMs(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return 15000;
                case FetchPresetMode.Balanced:
                    return 22000;
                case FetchPresetMode.Thorough:
                    return 45000;
                default:
                    return 45000;
            }
        }

        public static int GetPresetPrimaryProviderTimeoutMs(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return 4000;
                case FetchPresetMode.Balanced:
                    return 6000;
                case FetchPresetMode.Thorough:
                    return 10000;
                default:
                    return 10000;
            }
        }

        public static int GetPresetFallbackProviderTimeoutMs(FetchPresetMode mode)
        {
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return 2500;
                case FetchPresetMode.Balanced:
                    return 3500;
                case FetchPresetMode.Thorough:
                    return 5000;
                default:
                    return 5000;
            }
        }

        public static bool IsProviderEnabledByPreset(FetchPresetMode mode, string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return false;

            string normalized = NormalizeProviderName(providerName);
            switch (mode)
            {
                case FetchPresetMode.Fast:
                    return normalized == "Direct Site" ||
                           normalized == "Google" ||
                           normalized == "Twenty Icons";
                case FetchPresetMode.Balanced:
                    return normalized == "Direct Site" ||
                           normalized == "Google" ||
                           normalized == "Favicone";
                case FetchPresetMode.Thorough:
                    return true;
                default:
                    return true;
            }
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
    }

    public enum FetchPresetMode
    {
        Custom = 0,
        Fast = 1,
        Balanced = 2,
        Thorough = 3
    }
}
