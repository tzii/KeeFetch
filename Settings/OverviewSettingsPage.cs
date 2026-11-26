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

                cmbProfile.Items.Add(new ProfileListItem(profile.Id,
                    ProfilePresentation.ChoiceName(profile)));
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

        /// <summary>Selects a profile as if the user picked it, raising ProfileChanged.</summary>
        public void ChooseProfile(string profileId)
        {
            SelectProfile(profileId);
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
            string profileId = item == null ? ProfilePresentation.CustomProfileId : item.Id;

            lblRecommended.Text = ProfilePresentation.Summary(profileId);
            lblProfileDescription.Text = ProfilePresentation.ContactsLine(profileId) +
                Environment.NewLine + DescribeBudget(profileId);
        }

        private static string DescribeBudget(string profileId)
        {
            if (string.Equals(profileId, ProfilePresentation.CustomProfileId, StringComparison.OrdinalIgnoreCase))
                return "Timeouts and fallbacks are set on the Advanced tab.";

            try
            {
                FetchProfileDefinition profile = FetchProfileCatalog.GetRequiredProfile(profileId);
                return string.Format(
                    "Gives up on an entry after {0} seconds{1}.",
                    (profile.CumulativeTimeoutMs + 999) / 1000,
                    profile.StopAfterStrongResolved && profile.ProviderIds.Count > 1
                        ? " and stops at the first confident icon"
                        : string.Empty);
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }
    }
}
