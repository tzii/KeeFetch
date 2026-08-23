using System;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;

namespace KeeFetch.Settings
{
    internal sealed partial class OverviewSettingsPage : UserControl
    {
        private bool isLoading;
        private SettingsDraft boundDraft;

        public event EventHandler ProfileChanged;

        public OverviewSettingsPage()
        {
            InitializeComponent();
        }

        public void LoadFromDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            boundDraft = draft;
            isLoading = true;
            cmbProfile.Items.Clear();
            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles)
            {
                if (!profile.IsVisible)
                    continue;

                string displayName = profile.DisplayName;
                if (profile.Id.Equals("everyday", StringComparison.OrdinalIgnoreCase))
                    displayName += " (Recommended)";
                cmbProfile.Items.Add(new ProfileListItem(profile.Id, displayName));
            }
            cmbProfile.Items.Add(new ProfileListItem("custom", "Custom"));

            SelectProfile(draft.ProfileId);
            UpdateProfileDescription();
            isLoading = false;
        }

        public void SaveToDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            ProfileListItem selected = cmbProfile.SelectedItem as ProfileListItem;
            draft.ProfileId = selected != null ? selected.Id : "custom";
        }

        public void FocusControl(string controlKey)
        {
            if (string.Equals(controlKey, "profile", StringComparison.OrdinalIgnoreCase))
                cmbProfile.Focus();
        }

        private void SelectProfile(string profileId)
        {
            string target = string.IsNullOrWhiteSpace(profileId) ? "custom" : profileId.Trim();
            for (int i = 0; i < cmbProfile.Items.Count; i++)
            {
                ProfileListItem item = cmbProfile.Items[i] as ProfileListItem;
                if (item != null && item.Id.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    cmbProfile.SelectedIndex = i;
                    return;
                }
            }

            cmbProfile.SelectedIndex = cmbProfile.Items.Count - 1;
        }

        private void cmbProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isLoading)
                return;

            UpdateProfileDescription();
            if (boundDraft != null)
                SaveToDraft(boundDraft);

            EventHandler handler = ProfileChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void UpdateProfileDescription()
        {
            ProfileListItem item = cmbProfile.SelectedItem as ProfileListItem;
            if (item == null || item.Id.Equals("custom", StringComparison.OrdinalIgnoreCase))
            {
                lblProfileDescription.Text =
                    "Choose provider order, timeouts, and fallback behavior manually.";
                return;
            }

            try
            {
                FetchProfileDefinition profile = FetchProfileCatalog.GetRequiredProfile(item.Id);
                lblProfileDescription.Text = profile.IntendedUse + Environment.NewLine + profile.Description;
            }
            catch (InvalidOperationException)
            {
                lblProfileDescription.Text = string.Empty;
            }
        }
    }
}
