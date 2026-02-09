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
            numMaxIconSize.Value = config.MaxIconSize;
            numTimeout.Value = config.Timeout;
            txtIconPrefix.Text = config.IconNamePrefix;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.PrefixUrls = chkPrefixUrls.Checked;
            config.UseTitleField = chkUseTitleField.Checked;
            config.SkipExistingIcons = chkSkipExistingIcons.Checked;
            config.AutoSave = chkAutoSave.Checked;
            config.AllowSelfSignedCerts = chkAllowSelfSigned.Checked;
            config.MaxIconSize = (int)numMaxIconSize.Value;
            config.Timeout = (int)numTimeout.Value;
            config.IconNamePrefix = txtIconPrefix.Text;

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
