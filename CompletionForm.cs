using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.FetchProfiles;

namespace KeeFetch
{
    internal enum CompletionAction
    {
        Close,
        RetryEligible
    }

    internal sealed partial class CompletionForm : Form
    {
        private readonly BatchRunResult result;
        private readonly string summaryText;

        public CompletionForm(BatchRunResult result, bool retryAllowed)
        {
            if (result == null)
                throw new ArgumentNullException("result");

            this.result = result;
            InitializeComponent();

            SelectedAction = CompletionAction.Close;
            DiagnosticsPath = GetExistingDiagnosticsPath(result);
            summaryText = BuildSummaryText(result);
            LoadResult(retryAllowed);
        }

        public CompletionAction SelectedAction { get; private set; }
        public string DiagnosticsPath { get; private set; }

        internal static string BuildSummaryText(BatchRunResult result)
        {
            if (result == null)
                throw new ArgumentNullException("result");

            var lines = new List<string>();
            lines.Add("KeeFetch batch summary");
            lines.Add("Profile: " + GetProfileDisplay(result.ProfileId));
            lines.Add("Elapsed: " + FormatElapsed(result.Elapsed));
            lines.Add("Total: " + result.TotalCount);
            lines.Add("Updated: " + result.UpdatedCount);
            lines.Add("Skipped: " + result.SkippedCount);
            lines.Add("Not found: " + result.NotFoundCount);
            lines.Add("Errors: " +
                (result.RecoverableErrorCount + result.InvalidInputCount));
            lines.Add("Invalid input: " + result.InvalidInputCount);
            lines.Add("Cancelled entries: " + result.CancelledCount);
            lines.Add("Retry eligible: " + result.RetryEntries.Count);
            lines.Add("Run cancelled: " + (result.WasCancelled ? "Yes" : "No"));
            lines.Add("Diagnostics log: " + DisplayPath(result.DiagnosticsLogPath));
            lines.Add("Diagnostics CSV: " + DisplayPath(result.DiagnosticsCsvPath));
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private void LoadResult(bool retryAllowed)
        {
            lblProfileValue.Text = GetProfileDisplay(result.ProfileId);
            lblElapsedValue.Text = FormatElapsed(result.Elapsed);
            lblTotalValue.Text = result.TotalCount.ToString();
            lblUpdatedValue.Text = result.UpdatedCount.ToString();
            lblSkippedValue.Text = result.SkippedCount.ToString();
            lblNotFoundValue.Text = result.NotFoundCount.ToString();
            lblErrorsValue.Text =
                (result.RecoverableErrorCount + result.InvalidInputCount).ToString();
            lblCancelledValue.Text = result.CancelledCount.ToString();
            lblExplanation.Text = BuildExplanation(result);

            bool canRetry = retryAllowed && !result.WasCancelled &&
                result.RetryEntries.Count > 0;
            btnRetry.Enabled = canRetry;
            btnRetry.Visible = retryAllowed;
            if (!retryAllowed && result.RetryEntries.Count > 0)
            {
                lblRetryHint.Text =
                    "KeeFetch allows one retry per batch. Inspect diagnostics for remaining misses.";
            }
            else if (result.WasCancelled)
            {
                lblRetryHint.Text =
                    "Retry is unavailable for a cancelled run. Start a new download when ready.";
            }
            else if (result.RetryEntries.Count == 0)
            {
                lblRetryHint.Text = "No entries are eligible for retry.";
            }
            else
            {
                lblRetryHint.Text = string.Format(
                    "Retry {0} not-found or recoverable-error entr{1} once.",
                    result.RetryEntries.Count,
                    result.RetryEntries.Count == 1 ? "y" : "ies");
            }

            btnOpenDiagnostics.Enabled = DiagnosticsPath != null;
            lblDiagnostics.Text = DiagnosticsPath == null
                ? "Diagnostics: no file was created for this run."
                : "Diagnostics: " + DiagnosticsPath;
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            if (!btnRetry.Enabled)
                return;

            SelectedAction = CompletionAction.RetryEligible;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            SelectedAction = CompletionAction.Close;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnCopySummary_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(summaryText);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "KeeFetch could not copy the summary.\n\n" + ex.Message,
                    "KeeFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void btnOpenDiagnostics_Click(object sender, EventArgs e)
        {
            if (DiagnosticsPath == null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(DiagnosticsPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                TryCopyText(DiagnosticsPath);
                MessageBox.Show(
                    "KeeFetch could not open the diagnostics file. The exact path is shown below " +
                    "and was copied when possible:\n\n" + DiagnosticsPath + "\n\n" + ex.Message,
                    "KeeFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static string GetExistingDiagnosticsPath(BatchRunResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.DiagnosticsLogPath) &&
                File.Exists(result.DiagnosticsLogPath))
            {
                return result.DiagnosticsLogPath;
            }

            if (!string.IsNullOrWhiteSpace(result.DiagnosticsCsvPath) &&
                File.Exists(result.DiagnosticsCsvPath))
            {
                return result.DiagnosticsCsvPath;
            }
            return null;
        }

        private static string BuildExplanation(BatchRunResult result)
        {
            if (result.WasCancelled)
                return "The run was cancelled. Completed entries are listed below; no automatic retry is offered.";

            int failures = result.NotFoundCount + result.RecoverableErrorCount +
                result.InvalidInputCount;
            if (result.UpdatedCount > 0 && failures > 0)
                return "Some entries were updated, while others need attention or may be retried.";
            if (result.UpdatedCount == 0 && failures > 0)
                return "No entries were updated. Review the counts and diagnostics before trying again.";
            return "The batch completed without not-found or error outcomes.";
        }

        private static string GetProfileDisplay(string profileId)
        {
            try
            {
                FetchProfileDefinition profile =
                    FetchProfileCatalog.GetRequiredProfile(profileId);
                return profile.DisplayName + " (" + profile.Id + ")";
            }
            catch (InvalidOperationException)
            {
                return string.IsNullOrWhiteSpace(profileId) ? "Unknown" : profileId;
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            int totalHours = (int)elapsed.TotalHours;
            if (totalHours > 0)
            {
                return string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                    totalHours, elapsed.Minutes, elapsed.Seconds, elapsed.Milliseconds);
            }
            return string.Format("{0:00}:{1:00}.{2:000}",
                (int)elapsed.TotalMinutes, elapsed.Seconds, elapsed.Milliseconds);
        }

        private static string DisplayPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "(not available)" : path;
        }

        private static void TryCopyText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                Logger.Debug("CompletionForm.TryCopyText", ex);
            }
        }
    }
}
