using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;
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
                CollectionAssert.AreEqual(
                    FetchProfileCatalog.ManagedProfiles.Where(profile => profile.IsVisible)
                        .Select(profile => profile.Id).ToArray(),
                    form.ProfileIds.ToArray());
                CollectionAssert.AreEqual(new[] { "everyday", "privacy" }, form.ProfileIds.ToArray(),
                    "v1.4 offers Balanced and Privacy; Fast and Precise are retired.");
                Assert.AreEqual("everyday", form.SelectedProfileId);

                RadioButton balanced = GetControl<RadioButton>(form, "rdoProfile_everyday");
                Assert.IsTrue(balanced.Checked);
                StringAssert.Contains(balanced.Text, "(recommended)");
                Assert.IsFalse(GetControl<RadioButton>(form, "rdoProfile_privacy").Text.Contains("recommended"));
                StringAssert.Contains(GetControl<Label>(form, "lblProfile_privacy").Text,
                    "Contacts: only the entry's own website.");
            }
        }

        [TestMethod]
        public void FirstRunForm_RetiredOrUnknownProfileFallsBackToBalancedAndShowsNotice()
        {
            using (var retired = new FirstRunForm("bulk-fast", "KeeFetch 1.4 no longer offers the Fast profile."))
            using (var plain = new FirstRunForm("everyday"))
            {
                foreach (Form form in new Form[] { retired, plain })
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-4000, -4000);
                    form.Show();
                }

                Assert.AreEqual("everyday", retired.SelectedProfileId);
                Label notice = GetField<Label>(retired, "lblNotice");
                Assert.IsTrue(notice.Visible);
                StringAssert.Contains(notice.Text, "Fast");
                Assert.IsFalse(GetField<Label>(plain, "lblNotice").Visible);
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
        public void FirstRunForm_ConfirmedChoiceClearsTheRetiredProfileNotice()
        {
            var ace = new AceCustomConfig();
            ace.SetString("KeeFetch.FetchProfileId", "bulk-fast");
            var config = new Configuration(ace);
            Assert.AreEqual("everyday", config.FetchProfileId);
            Assert.AreEqual("Fast", config.RetiredProfileName);

            using (var form = new FirstRunForm(config.FetchProfileId, "notice"))
            {
                InvokeHandler(form, "btnCancel_Click");
                Assert.IsFalse(KeeFetchExt.ApplyFirstRunChoice(config, form, null));
            }
            Assert.AreEqual("Fast", config.RetiredProfileName, "Cancelling keeps the notice for next time.");

            using (var form = new FirstRunForm(config.FetchProfileId, "notice"))
            {
                InvokeHandler(form, "btnConfirm_Click");
                Assert.IsTrue(KeeFetchExt.ApplyFirstRunChoice(config, form, null));
            }
            Assert.IsNull(config.RetiredProfileName);
            Assert.IsNull(new Configuration(ace).RetiredProfileName);
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
                Assert.IsFalse(form.MaximizeBox);
                Assert.IsTrue(form.MinimumSize.Width >= 560);
                Assert.IsTrue(form.MinimumSize.Height >= 480);

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
                Assert.IsTrue(GetField<Button>(form, "btnConfirm").Bottom <=
                    GetField<FlowLayoutPanel>(form, "actionPanel").ClientSize.Height,
                    "Buttons must stay visible at the minimum form size.");
                Control root = GetField<TableLayoutPanel>(form, "rootLayout");
                Assert.IsTrue(GetField<FlowLayoutPanel>(form, "actionPanel").Bottom <= root.ClientSize.Height,
                    "The button row must not be pushed out of the window.");

                Button confirm = GetField<Button>(form, "btnConfirm");
                Button cancel = GetField<Button>(form, "btnCancel");
                Assert.AreSame(confirm, form.AcceptButton);
                Assert.AreSame(cancel, form.CancelButton);
                Assert.AreEqual(DialogResult.Cancel, cancel.DialogResult);
                Assert.IsFalse(string.IsNullOrWhiteSpace(confirm.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cancel.AccessibleName));
                foreach (string id in form.ProfileIds)
                {
                    RadioButton choice = GetControl<RadioButton>(form, "rdoProfile_" + id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(choice.AccessibleName));
                    Assert.IsTrue(choice.Bottom <= choice.Parent.ClientSize.Height);
                }
            }
        }

        private static void SelectProfile(FirstRunForm form, string profileId)
        {
            Assert.IsTrue(form.SelectProfile(profileId), "Profile " + profileId + " is not offered.");
            Assert.IsTrue(GetControl<RadioButton>(form, "rdoProfile_" + profileId).Checked);
        }

        private static T GetControl<T>(Form form, string name) where T : Control
        {
            Control[] matches = form.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length, "Expected one control named " + name + ".");
            T control = matches[0] as T;
            Assert.IsNotNull(control, name + " was not a " + typeof(T).Name + ".");
            return control;
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
