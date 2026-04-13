using System;
using System.Linq;
using System.Windows.Forms;

namespace KeeFetch
{
    public partial class SettingsForm : Form
    {
        private readonly Configuration config;

        public SettingsForm(Configuration config)
        {
            this.config = config;
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            chkPrefixUrls.Checked = config.PrefixUrls;
            chkUseTitleField.Checked = config.UseTitleField;
            chkSkipExistingIcons.Checked = config.SkipExistingIcons;
            chkAutoSave.Checked = config.AutoSave;
            chkAllowSelfSigned.Checked = config.AllowSelfSignedCerts;
            chkUseThirdPartyFallbacks.Checked = config.UseThirdPartyFallbacks;
            chkAllowSyntheticFallbacks.Checked = config.AllowSyntheticFallbacks;
            numMaxIconSize.Value = config.MaxIconSize;
            numTimeout.Value = config.Timeout;
            txtIconPrefix.Text = config.IconNamePrefix;

            chkProviderDirectSite.Checked = config.EnableDirectSiteProvider;
            chkProviderTwentyIcons.Checked = config.EnableTwentyIconsProvider;
            chkProviderDuckDuckGo.Checked = config.EnableDuckDuckGoProvider;
            chkProviderGoogle.Checked = config.EnableGoogleProvider;
            chkProviderYandex.Checked = config.EnableYandexProvider;
            chkProviderFavicone.Checked = config.EnableFaviconeProvider;
            chkProviderIconHorse.Checked = config.EnableIconHorseProvider;

            LoadProviderOrderList();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.PrefixUrls = chkPrefixUrls.Checked;
            config.UseTitleField = chkUseTitleField.Checked;
            config.SkipExistingIcons = chkSkipExistingIcons.Checked;
            config.AutoSave = chkAutoSave.Checked;
            config.AllowSelfSignedCerts = chkAllowSelfSigned.Checked;
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
            foreach (string provider in FaviconDownloader.DefaultProviderOrder)
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

            btnProviderUp.Enabled = hasSelection && index > 0;
            btnProviderDown.Enabled = hasSelection && index < lstProviderOrder.Items.Count - 1;
        }
    }
}
