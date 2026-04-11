using System;
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
            txtProviderOrder.Text = config.ProviderOrder;
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
            config.ProviderOrder = txtProviderOrder.Text;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
