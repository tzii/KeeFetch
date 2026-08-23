using System;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class DownloadSettingsPage : UserControl
    {
        public DownloadSettingsPage()
        {
            InitializeComponent();
        }

        public void LoadFromDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            chkPrefixUrls.Checked = draft.PrefixUrls;
            chkUseTitleField.Checked = draft.UseTitleField;
            chkSkipExistingIcons.Checked = draft.SkipExistingIcons;
            chkAutoSave.Checked = draft.AutoSave;
            numMaxIconSize.Value = Math.Max(numMaxIconSize.Minimum,
                Math.Min(numMaxIconSize.Maximum, draft.MaxIconSize));
            txtIconNamePrefix.Text = draft.IconNamePrefix ?? string.Empty;
        }

        public void SaveToDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            draft.PrefixUrls = chkPrefixUrls.Checked;
            draft.UseTitleField = chkUseTitleField.Checked;
            draft.SkipExistingIcons = chkSkipExistingIcons.Checked;
            draft.AutoSave = chkAutoSave.Checked;
            draft.MaxIconSize = (int)numMaxIconSize.Value;
            draft.IconNamePrefix = txtIconNamePrefix.Text;
        }

        public void FocusControl(string controlKey)
        {
            if (string.Equals(controlKey, "max-icon-size", StringComparison.OrdinalIgnoreCase))
                numMaxIconSize.Focus();
            else if (string.Equals(controlKey, "icon-name-prefix", StringComparison.OrdinalIgnoreCase))
                txtIconNamePrefix.Focus();
            else if (string.Equals(controlKey, "prefix-urls", StringComparison.OrdinalIgnoreCase))
                chkPrefixUrls.Focus();
        }
    }
}
