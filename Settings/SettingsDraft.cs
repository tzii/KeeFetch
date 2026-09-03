using System;
using System.Collections.Generic;
using System.Linq;
using KeeFetch.FetchProfiles;

namespace KeeFetch.Settings
{
    internal sealed class SettingsValidationError
    {
        public SettingsValidationError(string pageId, string controlKey, string message)
        {
            PageId = pageId;
            ControlKey = controlKey;
            Message = message;
        }

        public string PageId { get; private set; }
        public string ControlKey { get; private set; }
        public string Message { get; private set; }
    }

    internal sealed class SettingsDraft
    {
        private readonly List<string> providerOrder = new List<string>();
        private readonly Dictionary<string, bool> providerEnabled =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private string profileId;

        private SettingsDraft()
        {
            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
                providerEnabled[provider.Id] = false;
        }

        public string ProfileId
        {
            get { return profileId; }
            set
            {
                profileId = value == null ? null : value.Trim();

                FetchProfileDefinition profile;
                if (TryGetManagedProfile(profileId, out profile))
                {
                    profileId = profile.Id;
                    ApplyManagedProfile(profile);
                }
                else if (string.Equals(profileId, "custom", StringComparison.OrdinalIgnoreCase))
                {
                    profileId = "custom";
                }
            }
        }

        public bool PrefixUrls { get; set; }
        public bool UseTitleField { get; set; }
        public bool SkipExistingIcons { get; set; }
        public bool AutoSave { get; set; }
        public bool AllowSelfSignedCerts { get; set; }
        public bool UseThirdPartyFallbacks { get; set; }
        public bool AllowSyntheticFallbacks { get; set; }
        public bool HasSeenFirstRunDisclosure { get; set; }
        public int MaxIconSize { get; set; }
        public int Timeout { get; set; }
        public string IconNamePrefix { get; set; }
        public IList<string> ProviderOrder { get { return providerOrder; } }

        public static SettingsDraft FromConfiguration(Configuration config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            var draft = new SettingsDraft();
            draft.PrefixUrls = config.PrefixUrls;
            draft.UseTitleField = config.UseTitleField;
            draft.SkipExistingIcons = config.SkipExistingIcons;
            draft.AutoSave = config.AutoSave;
            draft.AllowSelfSignedCerts = config.AllowSelfSignedCerts;
            draft.UseThirdPartyFallbacks = config.UseThirdPartyFallbacks;
            draft.AllowSyntheticFallbacks = config.AllowSyntheticFallbacks;
            draft.HasSeenFirstRunDisclosure = config.HasSeenFirstRunDisclosure;
            draft.MaxIconSize = config.MaxIconSize;
            draft.Timeout = config.Timeout;
            draft.IconNamePrefix = config.IconNamePrefix;

            foreach (string providerName in config.GetProviderOrderList())
            {
                ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerName);
                if (provider != null)
                    draft.providerOrder.Add(provider.Id);
            }

            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
                draft.providerEnabled[provider.Id] = config.IsProviderEnabled(provider.DisplayName);

            draft.profileId = config.PreviewFetchProfileId();
            FetchProfileDefinition managedProfile;
            if (TryGetManagedProfile(draft.profileId, out managedProfile))
            {
                draft.profileId = managedProfile.Id;
                draft.ApplyManagedProfile(managedProfile);
            }
            else if (string.Equals(draft.profileId, "custom", StringComparison.OrdinalIgnoreCase))
            {
                draft.profileId = "custom";
            }

            return draft;
        }

        public bool IsProviderEnabled(string providerId)
        {
            ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerId);
            if (provider == null)
                return false;

            bool enabled;
            return providerEnabled.TryGetValue(provider.Id, out enabled) && enabled;
        }

        public void SetProviderEnabled(string providerId, bool enabled)
        {
            ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerId);
            if (provider == null)
                throw new ArgumentException("Unknown provider: " + providerId, "providerId");

            providerEnabled[provider.Id] = enabled;
        }

        public IReadOnlyList<SettingsValidationError> Validate()
        {
            var errors = new List<SettingsValidationError>();

            FetchProfileDefinition managedProfile;
            bool isCustom = string.Equals(profileId, "custom", StringComparison.OrdinalIgnoreCase);
            bool isManaged = TryGetManagedProfile(profileId, out managedProfile) && managedProfile.IsVisible;
            if (!isCustom && !isManaged)
            {
                errors.Add(new SettingsValidationError(
                    "overview", "profile", "Choose a supported fetch profile."));
            }

            if (MaxIconSize < 16 || MaxIconSize > 256)
            {
                errors.Add(new SettingsValidationError(
                    "downloads", "max-icon-size", "Maximum icon size must be between 16 and 256 pixels."));
            }

            if (Timeout < 5 || Timeout > 60)
            {
                errors.Add(new SettingsValidationError(
                    "advanced", "timeout", "Connection timeout must be between 5 and 60 seconds."));
            }

            var seenProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (providerOrder.Count == 0)
            {
                errors.Add(new SettingsValidationError(
                    "providers", "provider-order", "Provider order must contain at least one provider."));
            }

            foreach (string rawProviderId in providerOrder)
            {
                ProviderDefinition provider = FetchProfileCatalog.FindProvider(rawProviderId);
                if (provider == null)
                {
                    errors.Add(new SettingsValidationError(
                        "providers", "provider-order", "Provider order contains an unknown provider."));
                    continue;
                }

                if (!seenProviderIds.Add(provider.Id))
                {
                    errors.Add(new SettingsValidationError(
                        "providers", "provider-order", "Provider order cannot contain duplicate providers."));
                }
            }

            if (isCustom && !FetchProfileCatalog.Providers.Any(provider => IsProviderEnabled(provider.Id)))
            {
                errors.Add(new SettingsValidationError(
                    "providers", "enabled-providers", "Enable at least one provider for Custom mode."));
            }

            if (string.Equals(profileId, "privacy", StringComparison.OrdinalIgnoreCase))
            {
                bool hasThirdPartyOverride = UseThirdPartyFallbacks ||
                    FetchProfileCatalog.Providers.Any(provider =>
                        provider.IsThirdParty && IsProviderEnabled(provider.Id));

                if (hasThirdPartyOverride)
                {
                    errors.Add(new SettingsValidationError(
                        "providers", "third-party-providers",
                        "The Privacy profile cannot enable third-party providers."));
                }
            }

            return errors.AsReadOnly();
        }

        public void ApplyTo(Configuration config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            IReadOnlyList<SettingsValidationError> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Settings are invalid: " + string.Join(" ", errors.Select(error => error.Message).ToArray()));
            }

            config.PrefixUrls = PrefixUrls;
            config.UseTitleField = UseTitleField;
            config.SkipExistingIcons = SkipExistingIcons;
            config.AutoSave = AutoSave;
            config.AllowSelfSignedCerts = AllowSelfSignedCerts;
            bool effectiveThirdPartyFallbacks = UseThirdPartyFallbacks;
            if (string.Equals(profileId, "custom", StringComparison.OrdinalIgnoreCase))
            {
                effectiveThirdPartyFallbacks = FetchProfileCatalog.Providers.Any(provider =>
                    provider.IsThirdParty && IsProviderEnabled(provider.Id));
            }
            config.UseThirdPartyFallbacks = effectiveThirdPartyFallbacks;
            config.AllowSyntheticFallbacks = AllowSyntheticFallbacks;
            config.HasSeenFirstRunDisclosure = HasSeenFirstRunDisclosure;
            config.MaxIconSize = MaxIconSize;
            config.Timeout = Timeout;
            config.IconNamePrefix = IconNamePrefix;

            var providerDisplayOrder = new List<string>();
            foreach (string providerId in providerOrder)
            {
                ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerId);
                if (provider != null)
                    providerDisplayOrder.Add(provider.DisplayName);
            }
            config.ProviderOrder = string.Join(",", providerDisplayOrder.ToArray());

            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
                config.SetProviderEnabled(provider.DisplayName, IsProviderEnabled(provider.Id));

            config.FetchProfileId = IsManagedProfileId(profileId) ?
                FetchProfileCatalog.GetRequiredProfile(profileId).Id : "custom";
        }

        private void ApplyManagedProfile(FetchProfileDefinition profile)
        {
            providerOrder.Clear();
            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
                providerEnabled[provider.Id] = false;

            bool usesThirdParty = false;
            foreach (string providerId in profile.ProviderIds)
            {
                ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerId);
                if (provider == null)
                    continue;

                providerOrder.Add(provider.Id);
                providerEnabled[provider.Id] = true;
                if (provider.IsThirdParty)
                    usesThirdParty = true;
            }

            UseThirdPartyFallbacks = usesThirdParty;
            AllowSyntheticFallbacks = profile.AllowSyntheticFallbacks;
            Timeout = Math.Max(5, (profile.PrimaryTimeoutMs + 999) / 1000);
        }

        private static bool TryGetManagedProfile(string id, out FetchProfileDefinition profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            try
            {
                profile = FetchProfileCatalog.GetRequiredProfile(id);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool IsManagedProfileId(string id)
        {
            FetchProfileDefinition ignored;
            return TryGetManagedProfile(id, out ignored);
        }
    }
}
