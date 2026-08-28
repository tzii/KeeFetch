using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;
using KeeFetch.Settings;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class FirstRunFormTests
    {
        [TestMethod]
        public void FirstRunForm_ListsEveryVisibleProfileAndDefaultsToRecommended()
        {
            using (var form = new FirstRunForm("everyday"))
            {
                ListBox profiles = GetField<ListBox>(form, "lstProfiles");
                ProfileListItem[] items = profiles.Items.Cast<ProfileListItem>().ToArray();

                CollectionAssert.AreEqual(
                    FetchProfileCatalog.ManagedProfiles.Where(profile => profile.IsVisible)
                        .Select(profile => profile.Id).ToArray(),
                    items.Select(item => item.Id).ToArray());
                Assert.IsTrue(items.Any(item => item.Id == "privacy"));
                Assert.AreEqual("everyday", form.SelectedProfileId);
                StringAssert.Contains(((ProfileListItem)profiles.SelectedItem).DisplayName,
                    "Recommended");
            }
        }

        [TestMethod]
        public void FirstRunForm_SelectionWritesNothingUntilConfirmedChoiceIsApplied()
        {
            var config = new Configuration(new AceCustomConfig());
            string originalProfileId = config.FetchProfileId;

            using (var form = new FirstRunForm(originalProfileId))
            {
                SelectProfile(form, "privacy");

                Assert.AreEqual(originalProfileId, config.FetchProfileId);
                Assert.IsFalse(config.HasSeenFirstRunDisclosure);
                Assert.IsFalse(form.Confirmed);

                InvokeHandler(form, "btnConfirm_Click");

                Assert.IsTrue(form.Confirmed);
                Assert.AreEqual("privacy", form.SelectedProfileId);
                Assert.AreEqual(originalProfileId, config.FetchProfileId,
                    "The form must not write Configuration itself.");
                Assert.IsFalse(config.HasSeenFirstRunDisclosure);

                Assert.IsTrue(KeeFetchExt.ApplyFirstRunChoice(config, form, null));
                Assert.AreEqual("privacy", config.FetchProfileId);
                Assert.IsTrue(config.HasSeenFirstRunDisclosure);
            }
        }

        [TestMethod]
        public void FirstRunForm_CancelAbortsAndLeavesProfileAndDisclosureUnchanged()
        {
            var config = new Configuration(new AceCustomConfig());
            string originalProfileId = config.FetchProfileId;

            using (var form = new FirstRunForm(originalProfileId))
            {
                SelectProfile(form, "privacy");
                InvokeHandler(form, "btnCancel_Click");

                Assert.IsFalse(form.Confirmed);
                Assert.AreEqual(DialogResult.Cancel, form.DialogResult);
                Assert.IsFalse(KeeFetchExt.ApplyFirstRunChoice(config, form, null));
                Assert.AreEqual(originalProfileId, config.FetchProfileId);
                Assert.IsFalse(config.HasSeenFirstRunDisclosure);
            }
        }

        [TestMethod]
        public void FirstRunForm_ConfirmedChoicePersistsAfterCommit()
        {
            var config = new Configuration(new AceCustomConfig());

            int persistCount = 0;
            string profileAtPersist = null;
            bool disclosureAtPersist = false;
            Action persist = delegate
            {
                persistCount++;
                profileAtPersist = config.FetchProfileId;
                disclosureAtPersist = config.HasSeenFirstRunDisclosure;
            };

            using (var form = new FirstRunForm("everyday"))
            {
                SelectProfile(form, "privacy");
                InvokeHandler(form, "btnConfirm_Click");

                Assert.IsTrue(KeeFetchExt.ApplyFirstRunChoice(config, form, persist));
            }

            Assert.AreEqual(1, persistCount,
                "A confirmed first-run choice must persist exactly once.");
            Assert.AreEqual("privacy", profileAtPersist,
                "Persistence must run after the profile is committed in memory.");
            Assert.IsTrue(disclosureAtPersist,
                "Persistence must run after the disclosure flag is committed in memory.");
            Assert.AreEqual("privacy", config.FetchProfileId);
            Assert.IsTrue(config.HasSeenFirstRunDisclosure);
        }

        [TestMethod]
        public void FirstRunForm_CancelledChoiceNeverPersists()
        {
            var config = new Configuration(new AceCustomConfig());
            int persistCount = 0;
            Action persist = delegate { persistCount++; };

            using (var form = new FirstRunForm("everyday"))
            {
                InvokeHandler(form, "btnCancel_Click");

                Assert.IsFalse(KeeFetchExt.ApplyFirstRunChoice(config, form, persist));
            }

            Assert.AreEqual(0, persistCount,
                "A cancelled first-run choice must not trigger persistence.");
            Assert.IsFalse(config.HasSeenFirstRunDisclosure);
        }

        [TestMethod]
        public void FirstRunForm_PrivacyDisclosureNamesDomainSharingWithoutCredentialClaim()
        {
            using (var form = new FirstRunForm("everyday"))
            {
                Label disclosure = GetField<Label>(form, "lblPrivacyDisclosure");
                string text = disclosure.Text.ToLowerInvariant();

                StringAssert.Contains(text, "domain");
                StringAssert.Contains(text, "third-party");
                Assert.IsFalse(text.Contains("credentials are transmitted"));
                Assert.IsFalse(text.Contains("credentials may be sent"));

                LinkLabel privacyLink = GetField<LinkLabel>(form, "lnkPrivacy");
                Assert.IsFalse(string.IsNullOrWhiteSpace(privacyLink.Text));
                Assert.IsTrue(privacyLink.Links.Count > 0);
            }
        }

        [TestMethod]
        public void FirstRunForm_IsFontScaledResizableAndKeyboardAccessible()
        {
            using (var form = new FirstRunForm("everyday"))
            {
                Assert.AreEqual(AutoScaleMode.Font, form.AutoScaleMode);
                Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle);
                Assert.IsTrue(form.MinimumSize.Width >= 600);
                Assert.IsTrue(form.MinimumSize.Height >= 520);

                form.Size = form.MinimumSize;
                form.Show();
                Label disclosure = GetField<Label>(form, "lblPrivacyDisclosure");
                System.Drawing.Size measuredDisclosure = TextRenderer.MeasureText(
                    disclosure.Text,
                    disclosure.Font,
                    new System.Drawing.Size(disclosure.ClientSize.Width, int.MaxValue),
                    TextFormatFlags.WordBreak);
                Assert.IsTrue(measuredDisclosure.Height <= disclosure.ClientSize.Height,
                    "Privacy disclosure must not clip at the minimum form size.");

                Button confirm = GetField<Button>(form, "btnConfirm");
                Button cancel = GetField<Button>(form, "btnCancel");
                Assert.AreSame(confirm, form.AcceptButton);
                Assert.AreSame(cancel, form.CancelButton);
                Assert.AreEqual(DialogResult.Cancel, cancel.DialogResult);
                Assert.IsFalse(string.IsNullOrWhiteSpace(confirm.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cancel.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    GetField<ListBox>(form, "lstProfiles").AccessibleName));
            }
        }

        private static void SelectProfile(FirstRunForm form, string profileId)
        {
            ListBox profiles = GetField<ListBox>(form, "lstProfiles");
            profiles.SelectedItem = profiles.Items.Cast<ProfileListItem>()
                .Single(item => item.Id == profileId);
        }

        private static T GetField<T>(FirstRunForm form, string name) where T : class
        {
            FieldInfo field = typeof(FirstRunForm).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + ".");
            T value = field.GetValue(form) as T;
            Assert.IsNotNull(value, name + " was not a " + typeof(T).Name + ".");
            return value;
        }

        private static void InvokeHandler(FirstRunForm form, string name)
        {
            MethodInfo handler = typeof(FirstRunForm).GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, "Missing handler " + name + ".");
            handler.Invoke(form, new object[] { form, EventArgs.Empty });
        }
    }
}
