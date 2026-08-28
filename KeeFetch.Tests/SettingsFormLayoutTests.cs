using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KeeFetch.Settings;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class SettingsFormLayoutTests
    {
        [TestMethod]
        public void SettingsForm_HostsExactlyFourGuidedPagesWithSharedActionsOutsideTabs()
        {
            using (var form = NewForm())
            {
                TabControl tabs = GetField<TabControl>(form, "tabSettings");
                CollectionAssert.AreEqual(
                    new[] { "&Overview", "&Downloads", "&Providers", "&Advanced" },
                    tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray());

                Assert.IsInstanceOfType(tabs.TabPages[0].Controls[0], typeof(OverviewSettingsPage));
                Assert.IsInstanceOfType(tabs.TabPages[1].Controls[0], typeof(DownloadSettingsPage));
                Assert.IsInstanceOfType(tabs.TabPages[2].Controls[0], typeof(ProviderSettingsPage));
                Assert.IsInstanceOfType(tabs.TabPages[3].Controls[0], typeof(AdvancedSettingsPage));

                Button save = GetField<Button>(form, "btnSave");
                Button cancel = GetField<Button>(form, "btnCancel");
                Assert.IsFalse(IsDescendantOf(save, tabs));
                Assert.IsFalse(IsDescendantOf(cancel, tabs));
                Assert.AreSame(save, form.AcceptButton);
                Assert.AreSame(cancel, form.CancelButton);
                Assert.AreEqual(DialogResult.Cancel, cancel.DialogResult);
            }
        }

        [TestMethod]
        public void SettingsForm_UsesResizableAccessibleFontScaledLayout()
        {
            using (var form = NewForm())
            {
                form.CreateControl();
                form.PerformLayout();

                Assert.AreEqual(AutoScaleMode.Font, form.AutoScaleMode);
                Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle);
                Assert.IsTrue(form.MinimumSize.Width >= 640);
                Assert.IsTrue(form.MinimumSize.Height >= 480);

                TabControl tabs = GetField<TabControl>(form, "tabSettings");
                Button save = GetField<Button>(form, "btnSave");
                Button cancel = GetField<Button>(form, "btnCancel");
                Assert.IsFalse(string.IsNullOrWhiteSpace(tabs.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(save.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(cancel.AccessibleName));
                Assert.IsTrue(tabs.Right <= form.ClientSize.Width);
                Assert.IsTrue(cancel.Bottom <= form.ClientSize.Height);
            }
        }

        [TestMethod]
        public void SettingsForm_SaveCommitsDraftAndCancelLeavesConfigurationUntouched()
        {
            var saveConfig = new Configuration(new AceCustomConfig());
            using (var form = new SettingsForm(saveConfig))
            {
                OverviewSettingsPage overview = GetField<OverviewSettingsPage>(form, "overviewPage");
                DownloadSettingsPage downloads = GetField<DownloadSettingsPage>(form, "downloadPage");
                ComboBox profiles = Find<ComboBox>(overview, "cmbProfile");
                profiles.SelectedItem = profiles.Items.Cast<ProfileListItem>()
                    .Single(item => item.Id == "privacy");
                Find<CheckBox>(downloads, "chkAutoSave").Checked = true;

                InvokeHandler(form, "btnSave_Click");

                Assert.AreEqual(DialogResult.OK, form.DialogResult);
                Assert.AreEqual("privacy", saveConfig.FetchProfileId);
                Assert.IsTrue(saveConfig.AutoSave);
            }

            var cancelConfig = new Configuration(new AceCustomConfig());
            using (var form = new SettingsForm(cancelConfig))
            {
                DownloadSettingsPage downloads = GetField<DownloadSettingsPage>(form, "downloadPage");
                Find<CheckBox>(downloads, "chkAutoSave").Checked = true;

                InvokeHandler(form, "btnCancel_Click");

                Assert.AreEqual(DialogResult.Cancel, form.DialogResult);
                Assert.IsFalse(cancelConfig.AutoSave);
            }
        }

        [TestMethod]
        public void SettingsForm_ValidationSelectsErrorPageAndShowsAccessibleSummary()
        {
            using (var form = NewForm())
            {
                OverviewSettingsPage overview = GetField<OverviewSettingsPage>(form, "overviewPage");
                ComboBox profiles = Find<ComboBox>(overview, "cmbProfile");
                profiles.SelectedItem = profiles.Items.Cast<ProfileListItem>()
                    .Single(item => item.Id == "custom");
                form.Show();

                var errors = new[]
                {
                    new SettingsValidationError(
                        "advanced", "timeout", "Connection timeout is invalid.")
                };

                MethodInfo showErrors = typeof(SettingsForm).GetMethod(
                    "ShowValidationErrors", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(showErrors);
                showErrors.Invoke(form, new object[] { errors });

                TabControl tabs = GetField<TabControl>(form, "tabSettings");
                Label summary = GetField<Label>(form, "lblValidationSummary");
                AdvancedSettingsPage advanced = GetField<AdvancedSettingsPage>(form, "advancedPage");
                Assert.AreEqual("&Advanced", tabs.SelectedTab.Text);
                Assert.IsTrue(summary.Visible);
                StringAssert.Contains(summary.Text, "Connection timeout is invalid.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(summary.AccessibleName));
                Assert.IsTrue(Find<NumericUpDown>(advanced, "numTimeout").ContainsFocus);
            }
        }

        private static SettingsForm NewForm()
        {
            return new SettingsForm(new Configuration(new AceCustomConfig()));
        }

        private static T GetField<T>(SettingsForm form, string name) where T : class
        {
            FieldInfo field = typeof(SettingsForm).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + ".");
            T value = field.GetValue(form) as T;
            Assert.IsNotNull(value, name + " was not a " + typeof(T).Name + ".");
            return value;
        }

        private static T Find<T>(Control root, string name) where T : Control
        {
            Control[] matches = root.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length);
            T control = matches[0] as T;
            Assert.IsNotNull(control);
            return control;
        }

        private static bool IsDescendantOf(Control control, Control ancestor)
        {
            for (Control current = control.Parent; current != null; current = current.Parent)
            {
                if (current == ancestor)
                    return true;
            }
            return false;
        }

        private static void InvokeHandler(SettingsForm form, string name)
        {
            MethodInfo handler = typeof(SettingsForm).GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, "Missing handler " + name + ".");
            handler.Invoke(form, new object[] { form, EventArgs.Empty });
        }
    }
}
