using System;
using System.Collections.Generic;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;

namespace KeeFetch.Settings
{
    internal sealed partial class ProviderSettingsPage : UserControl
    {
        private bool isLoading;
        private bool isCustom;

        public ProviderSettingsPage()
        {
            InitializeComponent();
        }

        public void LoadFromDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");

            isLoading = true;
            clbProviders.Items.Clear();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string providerId in draft.ProviderOrder)
                AddProvider(draft, providerId, added);
            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
                AddProvider(draft, provider.Id, added);

            if (clbProviders.Items.Count > 0)
                clbProviders.SelectedIndex = 0;

            isCustom = string.Equals(draft.ProfileId, "custom", StringComparison.OrdinalIgnoreCase);
            clbProviders.Enabled = isCustom;
            btnProviderReset.Enabled = isCustom;
            lblProviderHint.Text = isCustom
                ? "Check enabled providers and move them into the order KeeFetch should try them."
                : "This profile manages provider selection and order. Choose Custom to edit them.";
            isLoading = false;
            UpdateMoveButtons();
        }

        public void SaveToDraft(SettingsDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException("draft");
            if (!string.Equals(draft.ProfileId, "custom", StringComparison.OrdinalIgnoreCase))
                return;

            draft.ProviderOrder.Clear();
            for (int i = 0; i < clbProviders.Items.Count; i++)
            {
                ProfileListItem item = clbProviders.Items[i] as ProfileListItem;
                if (item == null)
                    continue;
                draft.ProviderOrder.Add(item.Id);
                draft.SetProviderEnabled(item.Id, clbProviders.GetItemChecked(i));
            }
        }

        public void FocusControl(string controlKey)
        {
            if (string.Equals(controlKey, "provider-order", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controlKey, "enabled-providers", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controlKey, "third-party-providers", StringComparison.OrdinalIgnoreCase))
            {
                clbProviders.Focus();
            }
        }

        private void AddProvider(SettingsDraft draft, string providerId, HashSet<string> added)
        {
            ProviderDefinition provider = FetchProfileCatalog.FindProvider(providerId);
            if (provider == null || !added.Add(provider.Id))
                return;

            int index = clbProviders.Items.Add(
                new ProfileListItem(provider.Id, provider.DisplayName));
            clbProviders.SetItemChecked(index, draft.IsProviderEnabled(provider.Id));
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
            isLoading = true;
            clbProviders.Items.Clear();
            foreach (ProviderDefinition provider in FetchProfileCatalog.Providers)
            {
                int index = clbProviders.Items.Add(
                    new ProfileListItem(provider.Id, provider.DisplayName));
                clbProviders.SetItemChecked(index, true);
            }
            if (clbProviders.Items.Count > 0)
                clbProviders.SelectedIndex = 0;
            isLoading = false;
            UpdateMoveButtons();
        }

        private void clbProviders_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!isLoading)
                UpdateMoveButtons();
        }

        private void MoveSelectedProvider(int delta)
        {
            int index = clbProviders.SelectedIndex;
            int newIndex = index + delta;
            if (!isCustom || index < 0 || newIndex < 0 || newIndex >= clbProviders.Items.Count)
                return;

            object item = clbProviders.Items[index];
            bool isChecked = clbProviders.GetItemChecked(index);
            isLoading = true;
            clbProviders.Items.RemoveAt(index);
            clbProviders.Items.Insert(newIndex, item);
            clbProviders.SetItemChecked(newIndex, isChecked);
            clbProviders.SelectedIndex = newIndex;
            isLoading = false;
            UpdateMoveButtons();
        }

        private void UpdateMoveButtons()
        {
            int index = clbProviders.SelectedIndex;
            btnProviderUp.Enabled = isCustom && index > 0;
            btnProviderDown.Enabled = isCustom && index >= 0 && index < clbProviders.Items.Count - 1;
        }
    }
}
