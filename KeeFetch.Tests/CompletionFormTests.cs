using System;
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
        public void CompletionForm_ShowsHeadlineCountsAndOneRowPerEntryProblemsFirst()
        {
            using (var form = new CompletionForm(SampleResult(false, null, null), true))
            {
                Assert.AreEqual("Some icons could not be downloaded",
                    GetField<Label>(form, "lblTitle").Text);
                Assert.AreEqual(
                    "1 of 3 entries updated, 1 without an icon, 1 failed. Balanced profile, " +
                    (7.4).ToString("0.0") + " s.",
                    GetField<Label>(form, "lblSummary").Text);

                ListView list = GetField<ListView>(form, "lstOutcomes");
                Assert.AreEqual(3, list.Items.Count);
                Assert.AreEqual("Network", list.Items[0].Text);
                Assert.AreEqual("Failed", list.Items[0].SubItems[1].Text);
                Assert.AreEqual("Missing", list.Items[1].Text);
                Assert.AreEqual("No icon found", list.Items[1].SubItems[1].Text);
                Assert.AreEqual("Updated", list.Items[2].Text);
                Assert.AreEqual("Icon updated", list.Items[2].SubItems[1].Text);
                Assert.AreEqual("Website", list.Items[2].SubItems[2].Text);
                Assert.AreEqual("example.com", list.Items[2].SubItems[3].Text);
            }
        }

        [TestMethod]
        public void CompletionForm_TitleDescribesTheRunWithoutJargon()
        {
            Assert.AreEqual("Download cancelled", CompletionForm.BuildTitle(SampleResult(true, null, null)));
            Assert.AreEqual("Icons downloaded", CompletionForm.BuildTitle(CompleteResult()));
            Assert.AreEqual("Nothing to update", CompletionForm.BuildTitle(Result(
                Outcome("Skipped", BatchEntryStatus.Skipped))));
            Assert.AreEqual("No icons were downloaded", CompletionForm.BuildTitle(Result(
                Outcome("Missing", BatchEntryStatus.NotFound))));
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
                Assert.AreEqual("&Retry 2 entries", GetField<Button>(eligible, "btnRetry").Text);
                Assert.IsFalse(GetField<Button>(cancelled, "btnRetry").Enabled);
                Assert.IsFalse(GetField<Button>(exhausted, "btnRetry").Enabled);
                Assert.IsFalse(GetField<Button>(complete, "btnRetry").Enabled);
                StringAssert.Contains(GetField<Label>(eligible, "lblHint").Text, "Retry asks");
                StringAssert.Contains(GetField<Label>(exhausted, "lblHint").Text, "Export diagnostics");
            }
        }

        [TestMethod]
        public void CompletionForm_UndoIsOfferedOnlyWhenTheRunChangedSomething()
        {
            var changes = new RunChangeSet();
            changes.RecordChange(new AppliedIconChange(
                new PwEntry(true, true), PwUuid.Zero, new PwUuid(true), null));

            using (var unchanged = new CompletionForm(SampleResult(false, null, null), true))
            using (var changed = new CompletionForm(SampleResult(false, null, changes), true))
            {
                Assert.IsFalse(GetField<Button>(unchanged, "btnUndo").Enabled);
                Assert.IsTrue(GetField<Button>(changed, "btnUndo").Enabled);
                StringAssert.Contains(GetField<Label>(changed, "lblHint").Text, "Undo restores");

                InvokeHandler(changed, "btnUndo_Click");

                Assert.AreEqual(CompletionAction.Undo, changed.SelectedAction);
                Assert.AreEqual(DialogResult.OK, changed.DialogResult);
            }
        }

        [TestMethod]
        public void CompletionForm_ExportDiagnosticsRequiresRecordedDiagnostics()
        {
            var diagnostics = new DiagnosticsReport(new[] { "[#1] url=https://example.com/" }, null, null);

            using (var available = new CompletionForm(SampleResult(false, diagnostics, null), true))
            using (var unavailable = new CompletionForm(SampleResult(false, null, null), true))
            {
                Assert.IsTrue(GetField<Button>(available, "btnExportDiagnostics").Enabled);
                Assert.IsFalse(GetField<Button>(unavailable, "btnExportDiagnostics").Enabled);
            }
        }

        [TestMethod]
        public void BuildSummaryText_IsDeterministicPlainText()
        {
            string summary = CompletionForm.BuildSummaryText(
                SampleResult(false, null, null));

            Assert.AreEqual(
                "KeeFetch download summary" + Environment.NewLine +
                "Profile: Balanced" + Environment.NewLine +
                "Time: " + (7.4).ToString("0.0") + " s" + Environment.NewLine +
                "Entries: 3" + Environment.NewLine +
                "Updated: 1" + Environment.NewLine +
                "Skipped: 0" + Environment.NewLine +
                "No icon found: 1" + Environment.NewLine +
                "Failed: 1" + Environment.NewLine +
                "Cancelled: 0" + Environment.NewLine +
                "Can be retried: 2",
                summary);
        }

        [TestMethod]
        public void FormatElapsed_UsesShortHumanUnits()
        {
            Assert.AreEqual((0.4).ToString("0.0") + " s",
                CompletionForm.FormatElapsed(TimeSpan.FromMilliseconds(400)));
            Assert.AreEqual("42 s", CompletionForm.FormatElapsed(TimeSpan.FromSeconds(42)));
            Assert.AreEqual("1 min 5 s", CompletionForm.FormatElapsed(TimeSpan.FromSeconds(65)));
            Assert.AreEqual("1 h 2 min", CompletionForm.FormatElapsed(TimeSpan.FromMinutes(62)));
            Assert.AreEqual((0.0).ToString("0.0") + " s",
                CompletionForm.FormatElapsed(TimeSpan.FromSeconds(-1)));
        }

        [TestMethod]
        public void DescribeDetails_KeepsTechnicalProviderSummariesOutOfTheList()
        {
            BatchEntryOutcome notFound = Outcome("Missing", BatchEntryStatus.NotFound,
                "total=1; rejected=1; selected=none", false);
            BatchEntryOutcome timedOut = Outcome("Slow", BatchEntryStatus.RecoverableError,
                "total=2; timeout", true);
            BatchEntryOutcome exception = Outcome("Broken", BatchEntryStatus.RecoverableError,
                "The remote name could not be resolved.", false);

            Assert.AreEqual("None of the icon sources returned a usable icon.",
                CompletionForm.DescribeDetails(notFound));
            Assert.AreEqual("A request timed out or failed. Retrying may help.",
                CompletionForm.DescribeDetails(timedOut));
            Assert.AreEqual("The remote name could not be resolved.",
                CompletionForm.DescribeDetails(exception));
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
                // Voice control finds buttons by their visible text, and
                // screen readers read the summary itself.
                Assert.AreEqual(retry.Text.Replace("&", string.Empty), retry.AccessibleName);
                Label summary = GetField<Label>(form, "lblSummary");
                Assert.AreEqual(summary.Text, summary.AccessibilityObject.Name);

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
                Assert.IsFalse(form.MaximizeBox);
                Assert.IsTrue(form.MinimumSize.Width >= 600);
                Assert.IsTrue(form.MinimumSize.Height >= 400);

                form.Size = form.MinimumSize;
                form.Show();

                foreach (string fieldName in new[]
                {
                    "btnExportDiagnostics", "btnCopySummary", "btnRetry", "btnUndo", "btnClose"
                })
                {
                    Button button = GetField<Button>(form, fieldName);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(button.AccessibleName));
                    System.Drawing.Rectangle bounds = form.RectangleToClient(
                        button.RectangleToScreen(button.ClientRectangle));
                    Assert.IsTrue(bounds.Left >= 0, fieldName + " starts outside the form.");
                    Assert.IsTrue(bounds.Right <= form.ClientSize.Width, fieldName + " is cut off.");
                    Assert.IsTrue(bounds.Bottom <= form.ClientSize.Height, fieldName + " is cut off.");
                }
            }
        }

        private static BatchRunResult SampleResult(bool wasCancelled,
            DiagnosticsReport diagnostics, RunChangeSet changes)
        {
            return new BatchRunResult(
                new[]
                {
                    Outcome("Updated", BatchEntryStatus.Updated),
                    Outcome("Missing", BatchEntryStatus.NotFound),
                    Outcome("Network", BatchEntryStatus.RecoverableError)
                },
                wasCancelled,
                TimeSpan.FromMilliseconds(7400),
                "everyday",
                diagnostics,
                changes);
        }

        private static BatchRunResult CompleteResult()
        {
            return Result(Outcome("Updated", BatchEntryStatus.Updated));
        }

        private static BatchRunResult Result(params BatchEntryOutcome[] outcomes)
        {
            return new BatchRunResult(outcomes, false, TimeSpan.FromSeconds(1), "privacy", null, null);
        }

        private static BatchEntryOutcome Outcome(string title, BatchEntryStatus status)
        {
            return Outcome(title, status, status.ToString(), status == BatchEntryStatus.RecoverableError);
        }

        private static BatchEntryOutcome Outcome(string title, BatchEntryStatus status,
            string diagnostic, bool recoverable)
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
                recoverable,
                25,
                diagnostic);
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
