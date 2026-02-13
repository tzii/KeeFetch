using System;
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
        private int? maxIconSize;
        private int? timeout;
        private string iconNamePrefix;

        /// <summary>
        /// Initializes a new instance of the Configuration class.
        /// </summary>
        /// <param name="customConfig">The KeePass custom configuration.</param>
        public Configuration(AceCustomConfig customConfig)
        {
            config = customConfig;
        }

        /// <summary>
        /// Gets or sets whether to automatically prefix URLs with http:// or https://.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether to use the entry title field when URL is empty.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether to skip entries that already have custom icons.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether to automatically save the database after downloading icons.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether to allow self-signed SSL certificates.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether to use third-party fallback providers (Google, DuckDuckGo, etc.).
        /// </summary>
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

        /// <summary>
        /// Gets or sets the maximum icon size in pixels.
        /// </summary>
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
                maxIconSize = value;
                config.SetLong(Prefix + "MaxIconSize", value);
            }
        }

        /// <summary>
        /// Gets or sets the timeout for icon downloads in seconds (clamped between 5-60).
        /// </summary>
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

        /// <summary>
        /// Gets or sets the prefix for custom icon names in the database.
        /// </summary>
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
    }
}
