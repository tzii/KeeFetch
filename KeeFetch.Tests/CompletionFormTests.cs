using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.IconSelection;
using KeePassLib;
using KeePassLib.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class CompletionFormTests
    {
        [TestMethod]
        public void CompletionForm_DisplaysCountsProfileAndElapsedTime()
        {
            BatchRunResult result = SampleResult(false, null, null);

            using (var form = new CompletionForm(result, true))
            {
                Assert.AreEqual("Balanced (everyday)",
                    GetField<Label>(form, "lblProfileValue").Text);
                Assert.AreEqual("00:07.250",
                    GetField<Label>(form, "lblElapsedValue").Text);
                Assert.AreEqual("3", GetField<Label>(form, "lblTotalValue").Text);
                Assert.AreEqual("1", GetField<Label>(form, "lblUpdatedValue").Text);
                Assert.AreEqual("1", GetField<Label>(form, "lblNotFoundValue").Text);
                Assert.AreEqual("1", GetField<Label>(form, "lblErrorsValue").Text);
                StringAssert.Contains(GetField<Label>(form, "lblExplanation").Text,
                    "Some entries");
            }
        }

        [TestMethod]
        public void CompletionForm_RetryIsEnabledOnlyForEligibleNonCancelledFirstRun()
        {
            using (var eligible = new CompletionForm(SampleResult(false, null, null), true))
            using (var cancelled = new CompletionForm(SampleResult(true, null, null), true))
            using (var exhausted = new CompletionForm(SampleResult(false, null, null), false))
            using (var complete = new CompletionForm(CompleteResult(), true))
            {
                Assert.IsTrue(GetField<Button>(eligible, "btnRetry").Enabled);
                Assert.IsFalse(GetField<Button>(cancelled, "btnRetry").Enabled);
                Assert.IsFalse(GetField<Button>(exhausted, "btnRetry").Enabled);
                Assert.IsFalse(GetField<Button>(complete, "btnRetry").Enabled);
                StringAssert.Contains(GetField<Label>(exhausted, "lblRetryHint").Text,
                    "one retry");
            }
        }

        [TestMethod]
        public void CompletionForm_OpenDiagnosticsRequiresAnExistingResultPath()
        {
            string existing = Path.GetTempFileName();
            string missing = existing + ".missing";
            try
            {
                using (var available = new CompletionForm(
                    SampleResult(false, existing, missing), true))
                using (var unavailable = new CompletionForm(
                    SampleResult(false, missing, null), true))
                {
                    Assert.IsTrue(GetField<Button>(available, "btnOpenDiagnostics").Enabled);
                    Assert.AreEqual(existing, available.DiagnosticsPath);
                    Assert.IsFalse(GetField<Button>(unavailable, "btnOpenDiagnostics").Enabled);
                    Assert.IsNull(unavailable.DiagnosticsPath);
                }
            }
            finally
            {
                File.Delete(existing);
            }
        }

        [TestMethod]
        public void BuildSummaryText_IsDeterministicPlainText()
        {
            string summary = CompletionForm.BuildSummaryText(
                SampleResult(false, null, null));

            Assert.AreEqual(
                "KeeFetch batch summary" + Environment.NewLine +
                "Profile: Balanced (everyday)" + Environment.NewLine +
                "Elapsed: 00:07.250" + Environment.NewLine +
                "Total: 3" + Environment.NewLine +
                "Updated: 1" + Environment.NewLine +
                "Skipped: 0" + Environment.NewLine +
                "Not found: 1" + Environment.NewLine +
                "Errors: 1" + Environment.NewLine +
                "Invalid input: 0" + Environment.NewLine +
                "Cancelled entries: 0" + Environment.NewLine +
                "Retry eligible: 2" + Environment.NewLine +
                "Run cancelled: No" + Environment.NewLine +
                "Diagnostics log: (not available)" + Environment.NewLine +
                "Diagnostics CSV: (not available)",
                summary);
        }

        [TestMethod]
        public void CompletionForm_CloseIsDefaultAndRetryReturnsExplicitAction()
        {
            using (var form = new CompletionForm(SampleResult(false, null, null), true))
            {
                Button close = GetField<Button>(form, "btnClose");
                Button retry = GetField<Button>(form, "btnRetry");

                Assert.AreEqual(CompletionAction.Close, form.SelectedAction);
                Assert.AreSame(close, form.AcceptButton);
                Assert.AreSame(close, form.CancelButton);
                Assert.AreEqual(DialogResult.Cancel, close.DialogResult);
                Assert.IsFalse(string.IsNullOrWhiteSpace(close.AccessibleName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(retry.AccessibleName));

                InvokeHandler(form, "btnRetry_Click");

                Assert.AreEqual(CompletionAction.RetryEligible, form.SelectedAction);
                Assert.AreEqual(DialogResult.OK, form.DialogResult);
            }
        }

        [TestMethod]
        public void CompletionForm_IsFontScaledAccessibleAndContainedAtMinimumSize()
        {
            using (var form = new CompletionForm(SampleResult(false, null, null), true))
            {
                Assert.AreEqual(AutoScaleMode.Font, form.AutoScaleMode);
                Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle);
                Assert.IsTrue(form.MinimumSize.Width >= 600);
                Assert.IsTrue(form.MinimumSize.Height >= 480);

                form.Size = form.MinimumSize;
                form.Show();

                foreach (string fieldName in new[]
                {
                    "btnOpenDiagnostics", "btnCopySummary", "btnRetry", "btnClose"
                })
                {
                    Button button = GetField<Button>(form, fieldName);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(button.AccessibleName));
                    Assert.IsTrue(button.Right <= form.ClientSize.Width);
                    Assert.IsTrue(button.Bottom <= form.ClientSize.Height);
                }
            }
        }

        private static BatchRunResult SampleResult(bool wasCancelled,
            string diagnosticsLogPath, string diagnosticsCsvPath)
        {
            return new BatchRunResult(
                new[]
                {
                    Outcome("Updated", BatchEntryStatus.Updated),
                    Outcome("Missing", BatchEntryStatus.NotFound),
                    Outcome("Network", BatchEntryStatus.RecoverableError)
                },
                wasCancelled,
                TimeSpan.FromMilliseconds(7250),
                "everyday",
                diagnosticsLogPath,
                diagnosticsCsvPath);
        }

        private static BatchRunResult CompleteResult()
        {
            return new BatchRunResult(
                new[] { Outcome("Updated", BatchEntryStatus.Updated) },
                false,
                TimeSpan.FromSeconds(1),
                "privacy",
                null,
                null);
        }

        private static BatchEntryOutcome Outcome(string title, BatchEntryStatus status)
        {
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
            return new BatchEntryOutcome(
                entry,
                title,
                "https://example.com/",
                status,
                "direct-site",
                IconTier.SiteCanonical,
                false,
                status == BatchEntryStatus.RecoverableError,
                25,
                status.ToString());
        }

        private static T GetField<T>(CompletionForm form, string name) where T : class
        {
            FieldInfo field = typeof(CompletionForm).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + ".");
            T value = field.GetValue(form) as T;
            Assert.IsNotNull(value, name + " was not a " + typeof(T).Name + ".");
            return value;
        }

        private static void InvokeHandler(CompletionForm form, string name)
        {
            MethodInfo handler = typeof(CompletionForm).GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler, "Missing handler " + name + ".");
            handler.Invoke(form, new object[] { form, EventArgs.Empty });
        }
    }
}
