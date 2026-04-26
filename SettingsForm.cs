using System;
using System.Linq;
using System.Windows.Forms;

namespace KeeFetch
{
    public partial class SettingsForm : Form
    {
        private readonly Configuration config;
        private bool isLoadingSettings;

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

            cmbFetchPreset.SelectedItem = config.FetchPresetMode.ToString();
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
            config.FetchPresetMode = GetSelectedPresetMode();
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
            bool canEditOrder = GetSelectedPresetMode() == FetchPresetMode.Custom;

            btnProviderUp.Enabled = canEditOrder && hasSelection && index > 0;
            btnProviderDown.Enabled = canEditOrder && hasSelection && index < lstProviderOrder.Items.Count - 1;
        }

        private void LoadPresetOptions()
        {
            cmbFetchPreset.Items.Clear();
            cmbFetchPreset.Items.Add(FetchPresetMode.Balanced.ToString());
            cmbFetchPreset.Items.Add(FetchPresetMode.Fast.ToString());
            cmbFetchPreset.Items.Add(FetchPresetMode.Thorough.ToString());
            cmbFetchPreset.Items.Add(FetchPresetMode.Custom.ToString());
        }

        private void LoadNetworkAndProviderSettings()
        {
            var mode = GetSelectedPresetMode();
            if (mode == FetchPresetMode.Custom)
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
                ApplyPresetToControls(mode);
            }

            lblFetchPresetDescription.Text = Configuration.GetPresetDescription(mode);
            UpdatePresetManagedControlStates(mode);
        }

        private void ApplyPresetToControls(FetchPresetMode mode)
        {
            numTimeout.Value = Configuration.GetPresetTimeout(mode);
            chkUseThirdPartyFallbacks.Checked = Configuration.GetPresetUseThirdPartyFallbacks(mode);
            chkAllowSyntheticFallbacks.Checked = Configuration.GetPresetAllowSyntheticFallbacks(mode);

            chkProviderDirectSite.Checked = Configuration.IsProviderEnabledByPreset(mode, "Direct Site");
            chkProviderTwentyIcons.Checked = Configuration.IsProviderEnabledByPreset(mode, "Twenty Icons");
            chkProviderDuckDuckGo.Checked = Configuration.IsProviderEnabledByPreset(mode, "DuckDuckGo");
            chkProviderGoogle.Checked = Configuration.IsProviderEnabledByPreset(mode, "Google");
            chkProviderYandex.Checked = Configuration.IsProviderEnabledByPreset(mode, "Yandex");
            chkProviderFavicone.Checked = Configuration.IsProviderEnabledByPreset(mode, "Favicone");
            chkProviderIconHorse.Checked = Configuration.IsProviderEnabledByPreset(mode, "Icon Horse");

            lstProviderOrder.Items.Clear();
            foreach (string provider in GetProviderDisplayOrderForMode(mode))
                lstProviderOrder.Items.Add(provider);

            if (lstProviderOrder.Items.Count > 0)
                lstProviderOrder.SelectedIndex = 0;

            UpdateProviderOrderButtons();
        }

        private void UpdatePresetManagedControlStates(FetchPresetMode mode)
        {
            bool isCustom = mode == FetchPresetMode.Custom;

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

        private FetchPresetMode GetSelectedPresetMode()
        {
            if (cmbFetchPreset.SelectedItem == null)
                return FetchPresetMode.Custom;

            string selected = cmbFetchPreset.SelectedItem.ToString();
            FetchPresetMode parsed;
            if (Enum.TryParse(selected, true, out parsed))
                return parsed;
            return FetchPresetMode.Custom;
        }

        private System.Collections.Generic.List<string> GetProviderOrderForSelectedMode()
        {
            var mode = GetSelectedPresetMode();
            if (mode == FetchPresetMode.Custom)
                return new System.Collections.Generic.List<string>(FaviconDownloader.DefaultProviderOrder);

            return Configuration.GetPresetProviderOrderList(mode);
        }

        private System.Collections.Generic.List<string> GetProviderDisplayOrderForMode(FetchPresetMode mode)
        {
            var displayOrder = new System.Collections.Generic.List<string>();
            foreach (string provider in Configuration.GetPresetProviderOrderList(mode))
            {
                if (!displayOrder.Contains(provider))
                    displayOrder.Add(provider);
            }

            foreach (string provider in FaviconDownloader.DefaultProviderOrder)
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
