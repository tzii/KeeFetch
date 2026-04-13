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
    }
}
