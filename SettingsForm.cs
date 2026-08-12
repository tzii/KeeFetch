using System;
using System.Linq;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;

namespace KeeFetch
{
    public partial class SettingsForm : Form
    {
        private readonly Configuration config;
        private bool isLoadingSettings;

        private sealed class ProfileComboItem
        {
            public ProfileComboItem(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; private set; }
            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        public SettingsForm(Configuration config)
        {
            this.config = config;
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            isLoadingSettings = true;
            LoadPresetOptions();

            chkPrefixUrls.Checked = config.PrefixUrls;
            chkUseTitleField.Checked = config.UseTitleField;
            chkSkipExistingIcons.Checked = config.SkipExistingIcons;
            chkAutoSave.Checked = config.AutoSave;
            chkAllowSelfSigned.Checked = config.AllowSelfSignedCerts;
            numMaxIconSize.Value = config.MaxIconSize;
            txtIconPrefix.Text = config.IconNamePrefix;

            SelectProfileComboItem(config.FetchProfileId);
            LoadNetworkAndProviderSettings();
            isLoadingSettings = false;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.PrefixUrls = chkPrefixUrls.Checked;
            config.UseTitleField = chkUseTitleField.Checked;
            config.SkipExistingIcons = chkSkipExistingIcons.Checked;
            config.AutoSave = chkAutoSave.Checked;
            config.AllowSelfSignedCerts = chkAllowSelfSigned.Checked;
            config.FetchProfileId = GetSelectedProfileId();
            config.UseThirdPartyFallbacks = chkUseThirdPartyFallbacks.Checked;
            config.AllowSyntheticFallbacks = chkAllowSyntheticFallbacks.Checked;
            config.MaxIconSize = (int)numMaxIconSize.Value;
            config.Timeout = (int)numTimeout.Value;
            config.IconNamePrefix = txtIconPrefix.Text;

            config.EnableDirectSiteProvider = chkProviderDirectSite.Checked;
            config.EnableTwentyIconsProvider = chkProviderTwentyIcons.Checked;
            config.EnableDuckDuckGoProvider = chkProviderDuckDuckGo.Checked;
            config.EnableGoogleProvider = chkProviderGoogle.Checked;
            config.EnableYandexProvider = chkProviderYandex.Checked;
            config.EnableFaviconeProvider = chkProviderFavicone.Checked;
            config.EnableIconHorseProvider = chkProviderIconHorse.Checked;
            config.ProviderOrder = string.Join(",",
                lstProviderOrder.Items.Cast<object>().Select(item => item.ToString()));

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void LoadProviderOrderList()
        {
            lstProviderOrder.Items.Clear();
            foreach (string provider in config.GetProviderOrderList())
                lstProviderOrder.Items.Add(provider);

            if (lstProviderOrder.Items.Count > 0)
                lstProviderOrder.SelectedIndex = 0;

            UpdateProviderOrderButtons();
        }

        private void btnProviderUp_Click(object sender, EventArgs e)
        {
            MoveSelectedProvider(-1);
        }

        private void btnProviderDown_Click(object sender, EventArgs e)
        {
            MoveSelectedProvider(1);
        }

        private void btnProviderReset_Click(object sender, EventArgs e)
        {
            lstProviderOrder.Items.Clear();
            foreach (string provider in GetProviderOrderForSelectedMode())
                lstProviderOrder.Items.Add(provider);

            if (lstProviderOrder.Items.Count > 0)
                lstProviderOrder.SelectedIndex = 0;

            UpdateProviderOrderButtons();
        }

        private void lstProviderOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateProviderOrderButtons();
        }

        private void MoveSelectedProvider(int delta)
        {
            int index = lstProviderOrder.SelectedIndex;
            if (index < 0)
                return;

            int newIndex = index + delta;
            if (newIndex < 0 || newIndex >= lstProviderOrder.Items.Count)
                return;

            object item = lstProviderOrder.Items[index];
            lstProviderOrder.Items.RemoveAt(index);
            lstProviderOrder.Items.Insert(newIndex, item);
            lstProviderOrder.SelectedIndex = newIndex;
            UpdateProviderOrderButtons();
        }

        private void UpdateProviderOrderButtons()
        {
            int index = lstProviderOrder.SelectedIndex;
            bool hasSelection = index >= 0;
            bool canEditOrder = GetSelectedProfileId().Equals("custom", StringComparison.OrdinalIgnoreCase);

            btnProviderUp.Enabled = canEditOrder && hasSelection && index > 0;
            btnProviderDown.Enabled = canEditOrder && hasSelection && index < lstProviderOrder.Items.Count - 1;
        }

        private void LoadPresetOptions()
        {
            cmbFetchPreset.Items.Clear();
            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles)
            {
                if (profile.IsVisible)
                    cmbFetchPreset.Items.Add(new ProfileComboItem(profile.Id, profile.DisplayName));
            }

            cmbFetchPreset.Items.Add(new ProfileComboItem("custom", "Custom"));
        }

        private string GetSelectedProfileId()
        {
            ProfileComboItem selected = cmbFetchPreset.SelectedItem as ProfileComboItem;
            if (selected != null)
                return selected.Id;
            return "custom";
        }

        private FetchPresetMode GetSelectedPresetMode()
        {
            string id = GetSelectedProfileId();
            if (id.Equals("bulk-fast", StringComparison.OrdinalIgnoreCase)) return FetchPresetMode.Fast;
            if (id.Equals("everyday", StringComparison.OrdinalIgnoreCase)) return FetchPresetMode.Balanced;
            if (id.Equals("max-coverage", StringComparison.OrdinalIgnoreCase)) return FetchPresetMode.Thorough;
            return FetchPresetMode.Custom;
        }

        private void SelectProfileComboItem(string profileId)
        {
            string canonical = string.IsNullOrWhiteSpace(profileId) ? "custom" : profileId.Trim();
            for (int i = 0; i < cmbFetchPreset.Items.Count; i++)
            {
                ProfileComboItem item = cmbFetchPreset.Items[i] as ProfileComboItem;
                if (item != null && item.Id.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                {
                    cmbFetchPreset.SelectedIndex = i;
                    return;
                }
            }

            // Fallback: select Custom
            for (int i = 0; i < cmbFetchPreset.Items.Count; i++)
            {
                ProfileComboItem item = cmbFetchPreset.Items[i] as ProfileComboItem;
                if (item != null && item.Id.Equals("custom", StringComparison.OrdinalIgnoreCase))
                {
                    cmbFetchPreset.SelectedIndex = i;
                    return;
                }
            }
        }

        private FetchProfileDefinition GetSelectedProfile()
        {
            string id = GetSelectedProfileId();
            if (id.Equals("custom", StringComparison.OrdinalIgnoreCase))
                return null;
            try { return FetchProfileCatalog.GetRequiredProfile(id); }
            catch (InvalidOperationException) { return null; }
        }

        private void LoadNetworkAndProviderSettings()
        {
            FetchProfileDefinition profile = GetSelectedProfile();
            bool isCustom = profile == null;

            if (isCustom)
            {
                chkUseThirdPartyFallbacks.Checked = config.UseThirdPartyFallbacks;
                chkAllowSyntheticFallbacks.Checked = config.AllowSyntheticFallbacks;
                numTimeout.Value = config.Timeout;

                chkProviderDirectSite.Checked = config.EnableDirectSiteProvider;
                chkProviderTwentyIcons.Checked = config.EnableTwentyIconsProvider;
                chkProviderDuckDuckGo.Checked = config.EnableDuckDuckGoProvider;
                chkProviderGoogle.Checked = config.EnableGoogleProvider;
                chkProviderYandex.Checked = config.EnableYandexProvider;
                chkProviderFavicone.Checked = config.EnableFaviconeProvider;
                chkProviderIconHorse.Checked = config.EnableIconHorseProvider;

                LoadProviderOrderList();
            }
            else
            {
                ApplyProfileToControls(profile);
            }

            FetchPresetMode legacyMode = GetSelectedPresetMode();
            lblFetchPresetDescription.Text = Configuration.GetPresetDescription(legacyMode);
            UpdatePresetManagedControlStates(isCustom);
        }

        private void ApplyProfileToControls(FetchProfileDefinition profile)
        {
            numTimeout.Value = Math.Max(5, (profile.PrimaryTimeoutMs + 999) / 1000);
            chkUseThirdPartyFallbacks.Checked = true;
            chkAllowSyntheticFallbacks.Checked = profile.AllowSyntheticFallbacks;

            chkProviderDirectSite.Checked = false;
            chkProviderTwentyIcons.Checked = false;
            chkProviderDuckDuckGo.Checked = false;
            chkProviderGoogle.Checked = false;
            chkProviderYandex.Checked = false;
            chkProviderFavicone.Checked = false;
            chkProviderIconHorse.Checked = false;

            foreach (string pid in profile.ProviderIds)
            {
                ProviderDefinition p = FetchProfileCatalog.FindProvider(pid);
                string name = p != null ? p.DisplayName : pid;
                if (name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase)) chkProviderDirectSite.Checked = true;
                else if (name.Equals("Twenty Icons", StringComparison.OrdinalIgnoreCase)) chkProviderTwentyIcons.Checked = true;
                else if (name.Equals("DuckDuckGo", StringComparison.OrdinalIgnoreCase)) chkProviderDuckDuckGo.Checked = true;
                else if (name.Equals("Google", StringComparison.OrdinalIgnoreCase)) chkProviderGoogle.Checked = true;
                else if (name.Equals("Yandex", StringComparison.OrdinalIgnoreCase)) chkProviderYandex.Checked = true;
                else if (name.Equals("Favicone", StringComparison.OrdinalIgnoreCase)) chkProviderFavicone.Checked = true;
                else if (name.Equals("Icon Horse", StringComparison.OrdinalIgnoreCase)) chkProviderIconHorse.Checked = true;
            }

            lstProviderOrder.Items.Clear();
            foreach (string provider in GetProviderDisplayOrderForMode(GetSelectedPresetMode()))
                lstProviderOrder.Items.Add(provider);

            if (lstProviderOrder.Items.Count > 0)
                lstProviderOrder.SelectedIndex = 0;

            UpdateProviderOrderButtons();
        }

        private void UpdatePresetManagedControlStates(bool isCustom)
        {
            grpProviders.Text = isCustom
                ? "Provider Controls"
                : "Provider Controls (preset managed)";
            lblProviderOrderHint.Text = isCustom
                ? "Provider order: select one and move it up or down."
                : "Preset order: checked providers are used; unchecked providers are shown for reference.";

            numTimeout.Enabled = isCustom;
            chkUseThirdPartyFallbacks.Enabled = isCustom;
            chkAllowSyntheticFallbacks.Enabled = isCustom;

            chkProviderDirectSite.Enabled = isCustom;
            chkProviderTwentyIcons.Enabled = isCustom;
            chkProviderDuckDuckGo.Enabled = isCustom;
            chkProviderGoogle.Enabled = isCustom;
            chkProviderYandex.Enabled = isCustom;
            chkProviderFavicone.Enabled = isCustom;
            chkProviderIconHorse.Enabled = isCustom;
            lstProviderOrder.Enabled = isCustom;
            btnProviderReset.Enabled = isCustom;

            UpdateProviderOrderButtons();
        }

        private System.Collections.Generic.List<string> GetProviderOrderForSelectedMode()
        {
            FetchProfileDefinition profile = GetSelectedProfile();
            if (profile == null)
                return new System.Collections.Generic.List<string>(FetchProfileCatalog.DefaultProviderDisplayOrder);
            var list = new System.Collections.Generic.List<string>();
            foreach (string pid in profile.ProviderIds)
            {
                ProviderDefinition found = FetchProfileCatalog.FindProvider(pid);
                list.Add(found != null ? found.DisplayName : pid);
            }

            return list;
        }

        private System.Collections.Generic.List<string> GetProviderDisplayOrderForMode(FetchPresetMode mode)
        {
            var displayOrder = new System.Collections.Generic.List<string>();
            foreach (string provider in Configuration.GetPresetProviderOrderList(mode))
            {
                if (!displayOrder.Contains(provider))
                    displayOrder.Add(provider);
            }

            foreach (string provider in FetchProfileCatalog.DefaultProviderDisplayOrder)
            {
                if (!displayOrder.Contains(provider))
                    displayOrder.Add(provider);
            }

            return displayOrder;
        }

        private void cmbFetchPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isLoadingSettings)
                return;

            LoadNetworkAndProviderSettings();
        }
    }
}
