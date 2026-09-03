using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;
using KeeFetch.Settings;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class SettingsPageTests
    {
        [TestMethod]
        public void OverviewPage_ListsVisibleProfilesMarksRecommendedAndWritesOnlyDraft()
        {
            var config = new Configuration(new AceCustomConfig());
            SettingsDraft draft = SettingsDraft.FromConfiguration(config);

            using (var page = new OverviewSettingsPage())
            {
                page.LoadFromDraft(draft);
                ComboBox profiles = GetControl<ComboBox>(page, "cmbProfile");
                ProfileListItem[] items = profiles.Items.Cast<ProfileListItem>().ToArray();

                Assert.AreEqual(
                    FetchProfileCatalog.ManagedProfiles.Count(profile => profile.IsVisible) + 1,
                    items.Length);
                CollectionAssert.AreEqual(
                    FetchProfileCatalog.ManagedProfiles.Where(profile => profile.IsVisible)
                        .Select(profile => profile.Id).Concat(new[] { "custom" }).ToArray(),
                    items.Select(item => item.Id).ToArray());

                ProfileListItem recommended = items.Single(item => item.Id == "everyday");
                StringAssert.Contains(recommended.DisplayName, "Recommended");
                Assert.AreEqual("everyday", ((ProfileListItem)profiles.SelectedItem).Id);

                profiles.SelectedItem = items.Single(item => item.Id == "privacy");
                Assert.AreEqual("privacy", draft.ProfileId,
                    "Profile selection should update the in-memory draft immediately.");
                page.SaveToDraft(draft);

                Assert.AreEqual("privacy", draft.ProfileId);
                Assert.AreEqual("everyday", config.FetchProfileId,
                    "Pages must never write the persisted Configuration directly.");
            }
        }

        [TestMethod]
        public void DownloadPage_RoundTripsDownloadBehaviorThroughDraft()
        {
            SettingsDraft draft = CustomDraft();
            draft.PrefixUrls = false;
            draft.UseTitleField = false;
            draft.SkipExistingIcons = true;
            draft.AutoSave = true;
            draft.MaxIconSize = 96;
            draft.IconNamePrefix = "original-";

            using (var page = new DownloadSettingsPage())
            {
                page.LoadFromDraft(draft);

                Assert.IsFalse(GetControl<CheckBox>(page, "chkPrefixUrls").Checked);
                Assert.IsFalse(GetControl<CheckBox>(page, "chkUseTitleField").Checked);
                Assert.IsTrue(GetControl<CheckBox>(page, "chkSkipExistingIcons").Checked);
                Assert.IsTrue(GetControl<CheckBox>(page, "chkAutoSave").Checked);
                Assert.AreEqual(96m, GetControl<NumericUpDown>(page, "numMaxIconSize").Value);
                Assert.AreEqual("original-", GetControl<TextBox>(page, "txtIconNamePrefix").Text);

                GetControl<CheckBox>(page, "chkPrefixUrls").Checked = true;
                GetControl<CheckBox>(page, "chkUseTitleField").Checked = true;
                GetControl<CheckBox>(page, "chkSkipExistingIcons").Checked = false;
                GetControl<CheckBox>(page, "chkAutoSave").Checked = false;
                GetControl<NumericUpDown>(page, "numMaxIconSize").Value = 128;
                GetControl<TextBox>(page, "txtIconNamePrefix").Text = "saved-";
                page.SaveToDraft(draft);

                Assert.IsTrue(draft.PrefixUrls);
                Assert.IsTrue(draft.UseTitleField);
                Assert.IsFalse(draft.SkipExistingIcons);
                Assert.IsFalse(draft.AutoSave);
                Assert.AreEqual(128, draft.MaxIconSize);
                Assert.AreEqual("saved-", draft.IconNamePrefix);
            }
        }

        [TestMethod]
        public void ProviderPage_DisablesManagedEditingAndEnablesCustomEditing()
        {
            SettingsDraft draft = SettingsDraft.FromConfiguration(
                new Configuration(new AceCustomConfig()));

            using (var page = new ProviderSettingsPage())
            {
                page.LoadFromDraft(draft);
                CheckedListBox providers = GetControl<CheckedListBox>(page, "clbProviders");
                Assert.IsFalse(providers.Enabled);
                Assert.IsFalse(GetControl<Button>(page, "btnProviderReset").Enabled);

                draft.ProfileId = "custom";
                page.LoadFromDraft(draft);
                Assert.IsTrue(providers.Enabled);
                Assert.IsTrue(GetControl<Button>(page, "btnProviderReset").Enabled);
                Assert.AreEqual(FetchProfileCatalog.Providers.Count, providers.Items.Count);

                int googleIndex = FindProviderIndex(providers, "google");
                providers.SetItemChecked(googleIndex, false);
                providers.SelectedIndex = 1;
                GetControl<Button>(page, "btnProviderUp").PerformClick();
                page.SaveToDraft(draft);

                Assert.AreEqual(((ProfileListItem)providers.Items[0]).Id, draft.ProviderOrder[0]);
                Assert.IsFalse(draft.IsProviderEnabled("google"));
            }
        }

        [TestMethod]
        public void AdvancedPage_RoundTripsSecurityFallbackAndResetControls()
        {
            SettingsDraft draft = CustomDraft();
            draft.Timeout = 27;
            draft.AllowSelfSignedCerts = true;
            draft.AllowSyntheticFallbacks = false;

            using (var page = new AdvancedSettingsPage())
            {
                page.LoadFromDraft(draft);
                Assert.AreEqual(27m, GetControl<NumericUpDown>(page, "numTimeout").Value);
                Assert.IsTrue(GetControl<CheckBox>(page, "chkAllowSelfSigned").Checked);
                Assert.IsFalse(GetControl<CheckBox>(page, "chkAllowSyntheticFallbacks").Checked);

                GetControl<Button>(page, "btnResetAdvanced").PerformClick();
                Assert.AreEqual(15m, GetControl<NumericUpDown>(page, "numTimeout").Value);
                Assert.IsFalse(GetControl<CheckBox>(page, "chkAllowSelfSigned").Checked);
                Assert.IsTrue(GetControl<CheckBox>(page, "chkAllowSyntheticFallbacks").Checked);

                page.SaveToDraft(draft);
                Assert.AreEqual(15, draft.Timeout);
                Assert.IsFalse(draft.AllowSelfSignedCerts);
                Assert.IsTrue(draft.AllowSyntheticFallbacks);
            }
        }

        [TestMethod]
        public void SettingsPages_AllInteractiveControlsAreAccessibleWithDeterministicTabOrder()
        {
            Control[] pages =
            {
                new OverviewSettingsPage(),
                new DownloadSettingsPage(),
                new ProviderSettingsPage(),
                new AdvancedSettingsPage()
            };

            try
            {
                foreach (Control page in pages)
                {
                    AssertInteractiveControls(page);
                    AssertSiblingTabOrder(page);
                }
            }
            finally
            {
                foreach (Control page in pages)
                    page.Dispose();
            }
        }

        private static SettingsDraft CustomDraft()
        {
            SettingsDraft draft = SettingsDraft.FromConfiguration(
                new Configuration(new AceCustomConfig()));
            draft.ProfileId = "custom";
            return draft;
        }

        private static int FindProviderIndex(CheckedListBox list, string providerId)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                ProfileListItem item = list.Items[i] as ProfileListItem;
                if (item != null && item.Id == providerId)
                    return i;
            }

            Assert.Fail("Provider not found: " + providerId);
            return -1;
        }

        private static T GetControl<T>(Control root, string name) where T : Control
        {
            Control[] matches = root.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length, "Expected one control named " + name + ".");
            T control = matches[0] as T;
            Assert.IsNotNull(control, name + " was not a " + typeof(T).Name + ".");
            return control;
        }

        private static void AssertInteractiveControls(Control root)
        {
            foreach (Control control in Descendants(root).Where(IsInteractive))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(control.AccessibleName),
                    control.Name + " must have an accessible name.");
                Assert.IsTrue(control.TabIndex >= 0,
                    control.Name + " must have a non-negative tab index.");
            }
        }

        private static void AssertSiblingTabOrder(Control root)
        {
            foreach (Control parent in DescendantsAndSelf(root))
            {
                Control[] interactiveChildren = parent.Controls.Cast<Control>()
                    .Where(IsInteractive).ToArray();
                Assert.AreEqual(
                    interactiveChildren.Length,
                    interactiveChildren.Select(control => control.TabIndex).Distinct().Count(),
                    parent.Name + " has duplicate interactive child tab indices.");
            }
        }

        private static bool IsInteractive(Control control)
        {
            if (string.IsNullOrWhiteSpace(control.Name))
                return false;

            return control is ButtonBase || control is TextBoxBase ||
                control is ComboBox || control is ListBox || control is NumericUpDown;
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            return DescendantsAndSelf(root).Skip(1);
        }

        private static IEnumerable<Control> DescendantsAndSelf(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls)
            {
                foreach (Control descendant in DescendantsAndSelf(child))
                    yield return descendant;
            }
        }
    }
}
