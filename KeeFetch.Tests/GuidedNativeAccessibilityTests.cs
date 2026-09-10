using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeeFetch.Settings;
using KeePass.App.Configuration;
using KeePassLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class GuidedNativeAccessibilityTests
    {
        [TestMethod]
        public void GuidedNativeUi_AllInteractiveControlsAreNamedWithUniqueSiblingTabOrder()
        {
            foreach (Form form in CreateForms())
            {
                using (form)
                {
                    Assert.IsNotNull(form.AcceptButton, form.Name + " needs an Accept button.");
                    Assert.IsNotNull(form.CancelButton, form.Name + " needs a Cancel button.");
                    AssertInteractiveControls(form);
                    AssertSiblingTabOrder(form);
                }
            }
        }

        [TestMethod]
        public void GuidedNativeUi_EachFormExposesAnAccessKeyMnemonic()
        {
            foreach (Form form in CreateForms())
            {
                using (form)
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-4000, -4000);
                    form.Show();
                    Application.DoEvents();
                    AssertUniqueActiveMnemonics(form);
                }
            }
        }

        [TestMethod]
        public void SettingsUi_TabCaptionsRenderWithoutMnemonicMarkers()
        {
            using (var settings = new SettingsForm(
                new Configuration(new AceCustomConfig())))
            {
                TabControl tabs = Find<TabControl>(settings, "tabSettings");
                foreach (TabPage page in tabs.TabPages)
                {
                    Assert.IsFalse(page.Text.Contains("&"),
                        page.Name + " must not expose a literal mnemonic marker.");
                }
            }
        }

        [TestMethod]
        public void SettingsUi_MnemonicsAreUniqueForEverySelectedPage()
        {
            using (var settings = new SettingsForm(
                new Configuration(new AceCustomConfig())))
            {
                settings.StartPosition = FormStartPosition.Manual;
                settings.Location = new Point(-4000, -4000);
                settings.Show();
                TabControl tabs = Find<TabControl>(settings, "tabSettings");
                foreach (TabPage page in tabs.TabPages)
                {
                    tabs.SelectedTab = page;
                    Application.DoEvents();
                    AssertUniqueActiveMnemonics(settings);
                }
            }
        }

        [TestMethod]
        public void GuidedNativeUi_ContainsVisibleControlsAtSimulatedDpiScales()
        {
            foreach (float scale in new[] { 1F, 1.25F, 1.5F, 2F })
            {
                foreach (Form form in CreateForms())
                {
                    using (form)
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-4000, -4000);
                        form.Scale(new SizeF(scale, scale));
                        form.Show();
                        Application.DoEvents();

                        SettingsForm settings = form as SettingsForm;
                        if (settings != null)
                        {
                            TabControl tabs = Find<TabControl>(settings, "tabSettings");
                            foreach (TabPage page in tabs.TabPages)
                            {
                                tabs.SelectedTab = page;
                                settings.PerformLayout();
                                Application.DoEvents();
                                AssertContained(settings, scale);
                            }
                        }
                        else
                        {
                            AssertContained(form, scale);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void GuidedNativeUi_ProfileProviderAndPrivacyCopyFitsAtMinimumSize()
        {
            using (var settings = new SettingsForm(
                new Configuration(new AceCustomConfig())))
            {
                settings.Size = settings.MinimumSize;
                settings.Show();
                Application.DoEvents();

                AssertTextFits(Find<Label>(settings, "lblProfileDescription"));
                TabControl tabs = Find<TabControl>(settings, "tabSettings");
                tabs.SelectedTab = tabs.TabPages.Cast<TabPage>()
                    .Single(page => page.Text == "Providers");
                settings.PerformLayout();
                Application.DoEvents();
                AssertTextFits(Find<Label>(settings, "lblProviderHint"));
            }

            using (var firstRun = new FirstRunForm("everyday"))
            {
                firstRun.Size = firstRun.MinimumSize;
                firstRun.Show();
                Application.DoEvents();
                AssertTextFits(Find<Label>(firstRun, "lblProfileDescription"));
                AssertTextFits(Find<Label>(firstRun, "lblPrivacyDisclosure"));
            }
        }

        private static IEnumerable<Form> CreateForms()
        {
            yield return new SettingsForm(new Configuration(new AceCustomConfig()));
            yield return new FirstRunForm("everyday");
            yield return new CompletionForm(CompletionResult(), true);
        }

        private static BatchRunResult CompletionResult()
        {
            var entry = new PwEntry(true, true);
            var outcome = new BatchEntryOutcome(
                entry,
                "Example",
                "https://example.com/",
                BatchEntryStatus.NotFound,
                "direct-site",
                IconTier.Rejected,
                false,
                false,
                10,
                "No icon found.");
            return new BatchRunResult(
                new[] { outcome },
                false,
                TimeSpan.FromSeconds(1),
                "everyday",
                null,
                null);
        }

        private static void AssertInteractiveControls(Control root)
        {
            foreach (Control control in Descendants(root).Where(IsInteractive))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(control.AccessibleName),
                    root.Name + "/" + control.Name + " needs an accessible name.");
                Assert.IsTrue(control.TabIndex >= 0,
                    root.Name + "/" + control.Name + " needs a non-negative tab index.");
            }
        }

        private static void AssertSiblingTabOrder(Control root)
        {
            foreach (Control parent in DescendantsAndSelf(root))
            {
                Control[] interactive = parent.Controls.Cast<Control>()
                    .Where(IsInteractive).ToArray();
                Assert.AreEqual(
                    interactive.Length,
                    interactive.Select(control => control.TabIndex).Distinct().Count(),
                    parent.Name + " has duplicate interactive child tab indices.");
            }
        }

        private static void AssertContained(Control root, float scale)
        {
            foreach (Control parent in DescendantsAndSelf(root))
            {
                foreach (Control child in parent.Controls.Cast<Control>()
                    .Where(control => control.Visible &&
                        !string.IsNullOrWhiteSpace(control.Name)))
                {
                    Assert.IsTrue(child.Left >= 0 && child.Top >= 0,
                        ContainmentMessage(root, parent, child, scale));
                    Assert.IsTrue(child.Right <= parent.ClientSize.Width + 2,
                        ContainmentMessage(root, parent, child, scale));
                    Assert.IsTrue(child.Bottom <= parent.ClientSize.Height + 2,
                        ContainmentMessage(root, parent, child, scale));
                }
            }
        }

        private static string ContainmentMessage(Control root, Control parent,
            Control child, float scale)
        {
            return string.Format(
                "{0}/{1}/{2} escaped its parent at {3:P0}: child={4}, parent={5}.",
                root.Name,
                parent.Name,
                child.Name,
                scale,
                child.Bounds,
                parent.ClientRectangle);
        }

        private static void AssertTextFits(Label label)
        {
            Size measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                TextFormatFlags.WordBreak);
            Assert.IsTrue(measured.Height <= label.ClientSize.Height + 2,
                label.Name + " clips text at the minimum form size: measured " +
                measured + ", available " + label.ClientSize + ".");
        }

        private static bool IsInteractive(Control control)
        {
            if (string.IsNullOrWhiteSpace(control.Name))
                return false;

            return control is ButtonBase || control is TextBoxBase ||
                control is ComboBox || control is ListBox ||
                control is NumericUpDown || control is LinkLabel ||
                control is TabControl;
        }

        private static bool HasMnemonic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '&' && text[i + 1] != '&')
                    return true;
                if (text[i] == '&' && text[i + 1] == '&')
                    i++;
            }

            return false;
        }

        private static void AssertUniqueActiveMnemonics(Form form)
        {
            char[] keys = Descendants(form)
                .Where(control => control.Enabled &&
                    ((control is TabPage) ||
                     (control.Visible &&
                      (control is ButtonBase || control is Label))))
                .Select(control => MnemonicKey(control.Text))
                .Where(key => key != '\0')
                .ToArray();

            Assert.IsTrue(keys.Length > 0,
                form.Name + " needs an enabled visible access-key mnemonic.");
            Assert.AreEqual(keys.Length, keys.Distinct().Count(),
                form.Name + " has duplicate active access-key mnemonics: " +
                string.Join(", ", keys.Select(key => key.ToString()).ToArray()) + ".");
        }

        private static char MnemonicKey(string text)
        {
            if (string.IsNullOrEmpty(text))
                return '\0';

            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '&' && text[i + 1] != '&')
                    return char.ToUpperInvariant(text[i + 1]);
                if (text[i] == '&' && text[i + 1] == '&')
                    i++;
            }

            return '\0';
        }

        private static T Find<T>(Control root, string name) where T : Control
        {
            Control[] matches = root.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length, "Expected one control named " + name + ".");
            T result = matches[0] as T;
            Assert.IsNotNull(result, name + " was not a " + typeof(T).Name + ".");
            return result;
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
