using System;
using System.Diagnostics;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;
using KeeFetch.Settings;

namespace KeeFetch
{
    internal sealed partial class FirstRunForm : Form
    {
        internal const string PrivacyUrl = "https://tzii.github.io/KeeFetch/privacy.html";

        public FirstRunForm(string initialProfileId)
        {
            InitializeComponent();
            lnkPrivacy.Links.Add(0, lnkPrivacy.Text.Length, PrivacyUrl);
            LoadProfiles(initialProfileId);
        }

        public string SelectedProfileId { get; private set; }
        public bool Confirmed { get; private set; }

        private void LoadProfiles(string initialProfileId)
        {
            lstProfiles.Items.Clear();
            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles)
            {
                if (!profile.IsVisible)
                    continue;

                string displayName = profile.DisplayName;
                if (profile.Id.Equals("everyday", StringComparison.OrdinalIgnoreCase))
                    displayName += " (Recommended)";
                lstProfiles.Items.Add(new ProfileListItem(profile.Id, displayName));
            }

            string target = string.IsNullOrWhiteSpace(initialProfileId)
                ? "everyday"
                : initialProfileId.Trim();
            if (!SelectProfile(target))
                SelectProfile("everyday");
            if (lstProfiles.SelectedIndex < 0 && lstProfiles.Items.Count > 0)
                lstProfiles.SelectedIndex = 0;

            UpdateSelectedProfile();
        }

        private bool SelectProfile(string profileId)
        {
            for (int i = 0; i < lstProfiles.Items.Count; i++)
            {
                ProfileListItem item = lstProfiles.Items[i] as ProfileListItem;
                if (item != null && item.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
                {
                    lstProfiles.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        private void lstProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectedProfile();
        }

        private void UpdateSelectedProfile()
        {
            ProfileListItem item = lstProfiles.SelectedItem as ProfileListItem;
            SelectedProfileId = item == null ? null : item.Id;
            if (item == null)
            {
                lblProfileDescription.Text = string.Empty;
                btnConfirm.Enabled = false;
                return;
            }

            FetchProfileDefinition profile = FetchProfileCatalog.GetRequiredProfile(item.Id);
            lblProfileDescription.Text = profile.IntendedUse + Environment.NewLine +
                profile.Description;
            btnConfirm.Enabled = true;
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedProfileId))
                return;

            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void lnkPrivacy_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(PrivacyUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "KeeFetch could not open the privacy page. Open this address manually:\n" +
                    PrivacyUrl + "\n\n" + ex.Message,
                    "KeeFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
