using System;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class AdvancedSettingsPage : UserControl
    {
        private bool isCustom;

        public AdvancedSettingsPage()
        {
            InitializeComponent();
        }

        public void LoadFromDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            isCustom = string.Equals(draft.ProfileId, "custom", StringComparison.OrdinalIgnoreCase);
            numTimeout.Value = Math.Max(numTimeout.Minimum,
                Math.Min(numTimeout.Maximum, draft.Timeout));
            chkAllowSelfSigned.Checked = draft.AllowSelfSignedCerts;
            chkAllowSyntheticFallbacks.Checked = draft.AllowSyntheticFallbacks;
            numTimeout.Enabled = isCustom;
            chkAllowSyntheticFallbacks.Enabled = isCustom;
            lblManagedHint.Text = isCustom
                ? "Custom values apply after you save settings."
                : "The selected profile manages timeout and fallback behavior.";
        }

        public void SaveToDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            draft.AllowSelfSignedCerts = chkAllowSelfSigned.Checked;
            if (string.Equals(draft.ProfileId, "custom", StringComparison.OrdinalIgnoreCase))
            {
                draft.Timeout = (int)numTimeout.Value;
                draft.AllowSyntheticFallbacks = chkAllowSyntheticFallbacks.Checked;
            }
        }

        public void FocusControl(string controlKey)
        {
            if (string.Equals(controlKey, "timeout", StringComparison.OrdinalIgnoreCase))
                numTimeout.Focus();
            else if (string.Equals(controlKey, "self-signed", StringComparison.OrdinalIgnoreCase))
                chkAllowSelfSigned.Focus();
        }

        private void btnResetAdvanced_Click(object sender, EventArgs e)
        {
            chkAllowSelfSigned.Checked = false;
            if (isCustom)
            {
                numTimeout.Value = 15;
                chkAllowSyntheticFallbacks.Checked = true;
            }
        }
    }
}
