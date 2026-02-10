using System;
using KeePass.App.Configuration;

namespace KeeFetch
{
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
                    iconNamePrefix = config.GetString(Prefix + "IconNamePrefix", "kpif-");
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
