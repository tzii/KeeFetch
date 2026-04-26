using System;
using System.Reflection;
using System.Windows.Forms;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class SettingsFormLayoutTests
    {
        [TestMethod]
        public void SettingsForm_BalancedPreset_ShowsAllProvidersInOrderList()
        {
            using (var form = new SettingsForm(new Configuration(new AceCustomConfig())))
            {
                var list = GetControl<ListBox>(form, "lstProviderOrder");
                var hint = GetControl<Label>(form, "lblProviderOrderHint");

                Assert.AreEqual(FaviconDownloader.DefaultProviderOrder.Length, list.Items.Count);
                StringAssert.Contains(hint.Text, "checked providers are used");
            }
        }

        [TestMethod]
        public void SettingsForm_ProviderOrderControls_DoNotOverlapAndListShowsAllProviders()
        {
            using (var form = new SettingsForm(new Configuration(new AceCustomConfig())))
            {
                var list = GetControl<ListBox>(form, "lstProviderOrder");
                var up = GetControl<Button>(form, "btnProviderUp");
                var down = GetControl<Button>(form, "btnProviderDown");
                var reset = GetControl<Button>(form, "btnProviderReset");

                Assert.IsTrue(list.Height >= FaviconDownloader.DefaultProviderOrder.Length * 22,
                    "Provider order list should be tall enough to show every provider without scrolling.");
                Assert.IsTrue(up.Bottom < down.Top,
                    "Move Up and Move Down buttons should have visible spacing.");
                Assert.IsTrue(down.Bottom < reset.Top,
                    "Move Down and Reset buttons should have visible spacing.");
            }
        }

        [TestMethod]
        public void SettingsForm_LongSslCertificateText_FitsInsideNetworkGroup()
        {
            using (var form = new SettingsForm(new Configuration(new AceCustomConfig())))
            {
                var group = GetControl<GroupBox>(form, "grpNetwork");
                var ssl = GetControl<CheckBox>(form, "chkAllowSelfSigned");

                Assert.IsFalse(ssl.AutoSize,
                    "Long SSL warning text should use a bounded checkbox instead of autosizing past the group.");
                Assert.IsTrue(ssl.Right <= group.ClientSize.Width - 12,
                    "SSL warning checkbox should fit within the network group.");
            }
        }

        [TestMethod]
        public void SettingsForm_NumericRows_HaveSpacingBetweenLabelsInputsAndUnits()
        {
            using (var form = new SettingsForm(new Configuration(new AceCustomConfig())))
            {
                AssertNumericRowSpacing(
                    GetControl<Label>(form, "lblMaxIconSize"),
                    GetControl<NumericUpDown>(form, "numMaxIconSize"),
                    GetControl<Label>(form, "lblIconSizeUnit"));

                AssertNumericRowSpacing(
                    GetControl<Label>(form, "lblTimeout"),
                    GetControl<NumericUpDown>(form, "numTimeout"),
                    GetControl<Label>(form, "lblTimeoutUnit"));
            }
        }

        private static T GetControl<T>(SettingsForm form, string fieldName) where T : Control
        {
            FieldInfo field = typeof(SettingsForm).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + fieldName);

            var control = field.GetValue(form) as T;
            Assert.IsNotNull(control, "Field " + fieldName + " was not a " + typeof(T).Name);
            return control;
        }

        private static void AssertNumericRowSpacing(Label label, NumericUpDown input, Label unit)
        {
            Assert.IsTrue(label.Right + 8 <= input.Left,
                label.Name + " should not overlap its numeric input.");
            Assert.IsTrue(input.Right + 8 <= unit.Left,
                input.Name + " should not overlap its unit label.");
        }
    }
}
