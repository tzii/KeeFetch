using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.FetchProfiles;
using KeePassLib.Utility;

namespace KeeFetch
{
    internal enum CompletionAction
    {
        Close,
        RetryEligible,
        Undo
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
            UiAssets.PrepareDialog(this);

            SelectedAction = CompletionAction.Close;
            summaryText = BuildSummaryText(result);
            LoadResult(retryAllowed);
        }

        public CompletionAction SelectedAction { get; private set; }

        internal static string BuildSummaryText(BatchRunResult result)
        {
            if (result == null)
                throw new ArgumentNullException("result");

            var lines = new List<string>();
            lines.Add("KeeFetch download summary");
            lines.Add("Profile: " + ProfilePresentation.DisplayName(result.ProfileId));
            lines.Add("Time: " + FormatElapsed(result.Elapsed));
            lines.Add("Entries: " + result.TotalCount);
            lines.Add("Updated: " + result.UpdatedCount);
            lines.Add("Skipped: " + result.SkippedCount);
            lines.Add("No icon found: " + result.NotFoundCount);
            lines.Add("Failed: " + FailedCount(result));
            lines.Add("Cancelled: " + result.CancelledCount);
            lines.Add("Can be retried: " + result.RetryEntries.Count);
            if (result.WasCancelled)
                lines.Add("The download was cancelled before it finished.");
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        internal static string BuildTitle(BatchRunResult result)
        {
            if (result.WasCancelled)
                return "Download cancelled";
            if (result.UpdatedCount == 0 && FailedCount(result) == 0 && result.NotFoundCount == 0)
                return "Nothing to update";
            if (result.UpdatedCount == 0)
                return "No icons were downloaded";
            if (FailedCount(result) + result.NotFoundCount == 0)
                return "Icons downloaded";
            return "Some icons could not be downloaded";
        }

        /// <summary>One line of counts, e.g. "3 of 6 entries updated, 1 skipped, 2 without an icon. Balanced profile, 2.4 s."</summary>
        internal static string BuildCountsLine(BatchRunResult result)
        {
            var parts = new List<string>();
            parts.Add(string.Format("{0} of {1} {2} updated", result.UpdatedCount, result.TotalCount,
                result.TotalCount == 1 ? "entry" : "entries"));
            if (result.SkippedCount > 0)
                parts.Add(result.SkippedCount + " skipped");
            if (result.NotFoundCount > 0)
                parts.Add(result.NotFoundCount + " without an icon");
            if (FailedCount(result) > 0)
                parts.Add(FailedCount(result) + " failed");
            if (result.CancelledCount > 0)
                parts.Add(result.CancelledCount + " not processed");

            return string.Join(", ", parts.ToArray()) + ". " +
                ProfilePresentation.DisplayName(result.ProfileId) + " profile, " +
                FormatElapsed(result.Elapsed) + ".";
        }

        private void LoadResult(bool retryAllowed)
        {
            lblTitle.Text = BuildTitle(result);
            lblSummary.Text = BuildCountsLine(result);
            LoadOutcomes();

            int retryCount = result.RetryEntries.Count;
            bool canRetry = retryAllowed && !result.WasCancelled && retryCount > 0;
            btnRetry.Visible = retryAllowed;
            btnRetry.Enabled = canRetry;
            btnRetry.Text = retryCount > 0
                ? string.Format("&Retry {0} {1}", retryCount, retryCount == 1 ? "entry" : "entries")
                : "&Retry";
            btnRetry.AccessibleName = btnRetry.Text.Replace("&", string.Empty);

            btnUndo.Enabled = result.Changes.HasChanges;
            btnExportDiagnostics.Enabled = !result.Diagnostics.IsEmpty;
            lblHint.Text = BuildHint(result, retryAllowed);
            UpdateHintWidth();
            Resize += delegate { UpdateHintWidth(); };
        }

        internal static string BuildHint(BatchRunResult result, bool retryAllowed)
        {
            var sentences = new List<string>();
            int retryCount = result.RetryEntries.Count;
            if (result.WasCancelled)
                sentences.Add("Icons that were already downloaded have been kept.");
            else if (retryCount > 0 && retryAllowed)
                sentences.Add("Retry asks the icon sources again for entries that failed or had no icon.");
            else if (retryCount > 0)
                sentences.Add("Entries still without an icon may have no icon online. Export diagnostics to see why.");

            if (result.Changes.HasChanges)
                sentences.Add("Undo restores the icons these entries had before.");

            return string.Join(" ", sentences.ToArray());
        }

        private void LoadOutcomes()
        {
            lstOutcomes.BeginUpdate();
            try
            {
                lstOutcomes.Items.Clear();
                foreach (BatchEntryOutcome outcome in OrderForDisplay(result.Outcomes))
                {
                    var item = new ListViewItem(string.IsNullOrEmpty(outcome.Title) ? "(untitled)" : outcome.Title);
                    item.SubItems.Add(DescribeStatus(outcome.Status));
                    item.SubItems.Add(outcome.Status == BatchEntryStatus.Updated
                        ? DescribeSource(outcome)
                        : string.Empty);
                    string details = DescribeDetails(outcome);
                    item.SubItems.Add(details);
                    item.ToolTipText = details;
                    item.Tag = outcome;
                    lstOutcomes.Items.Add(item);
                }
            }
            finally
            {
                // -2 stretches the last column to the list's remaining width.
                colDetails.Width = -2;
                lstOutcomes.EndUpdate();
            }
        }

        /// <summary>Entries that need attention first, then the rest, each group in run order.</summary>
        internal static IList<BatchEntryOutcome> OrderForDisplay(IEnumerable<BatchEntryOutcome> outcomes)
        {
            var ordered = new List<BatchEntryOutcome>();
            foreach (BatchEntryStatus status in new[]
            {
                BatchEntryStatus.RecoverableError,
                BatchEntryStatus.NotFound,
                BatchEntryStatus.InvalidInput,
                BatchEntryStatus.Cancelled,
                BatchEntryStatus.Skipped,
                BatchEntryStatus.Updated
            })
            {
                foreach (BatchEntryOutcome outcome in outcomes)
                {
                    if (outcome.Status == status)
                        ordered.Add(outcome);
                }
            }

            return ordered;
        }

        internal static string DescribeStatus(BatchEntryStatus status)
        {
            switch (status)
            {
                case BatchEntryStatus.Updated: return "Icon updated";
                case BatchEntryStatus.Skipped: return "Skipped";
                case BatchEntryStatus.NotFound: return "No icon found";
                case BatchEntryStatus.RecoverableError: return "Failed";
                case BatchEntryStatus.InvalidInput: return "No web address";
                case BatchEntryStatus.Cancelled: return "Not processed";
                default: return status.ToString();
            }
        }

        private static string DescribeSource(BatchEntryOutcome outcome)
        {
            string source;
            ProviderDefinition provider = FetchProfileCatalog.FindProvider(outcome.ProviderId);
            if (provider == null)
                source = string.IsNullOrEmpty(outcome.ProviderId) ? string.Empty : outcome.ProviderId;
            else if (provider.IsThirdParty)
                source = provider.DisplayName;
            else
                source = "Website";

            return outcome.Synthetic ? source + " (generated)" : source;
        }

        /// <summary>
        /// A plain-language reason for the outcome. Technical provider summaries
        /// stay in the exported diagnostics.
        /// </summary>
        internal static string DescribeDetails(BatchEntryOutcome outcome)
        {
            switch (outcome.Status)
            {
                case BatchEntryStatus.Updated:
                    return HostOf(outcome.ResolvedUrl);
                case BatchEntryStatus.NotFound:
                    return "None of the icon sources returned a usable icon.";
                case BatchEntryStatus.RecoverableError:
                    if (outcome.Recoverable || string.IsNullOrWhiteSpace(outcome.Diagnostic))
                        return "A request timed out or failed. Retrying may help.";
                    return outcome.Diagnostic;
                default:
                    return outcome.Diagnostic ?? string.Empty;
            }
        }

        private static string HostOf(string url)
        {
            Uri uri;
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri) &&
                !string.IsNullOrEmpty(uri.Host))
            {
                return uri.Host;
            }

            return FaviconDiagnostics.RedactUrl(url);
        }

        private static int FailedCount(BatchRunResult result)
        {
            return result.RecoverableErrorCount + result.InvalidInputCount;
        }

        internal static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            if (elapsed.TotalSeconds < 60)
            {
                return elapsed.TotalSeconds.ToString(
                    elapsed.TotalSeconds < 10 ? "0.0" : "0", CultureInfo.CurrentCulture) + " s";
            }

            if (elapsed.TotalHours < 1)
                return string.Format("{0} min {1} s", (int)elapsed.TotalMinutes, elapsed.Seconds);

            return string.Format("{0} h {1} min", (int)elapsed.TotalHours, elapsed.Minutes);
        }

        private void UpdateHintWidth()
        {
            int available = ClientSize.Width - rootLayout.Padding.Horizontal;
            if (available <= 0)
                return;

            System.Drawing.Size max = new System.Drawing.Size(available, 0);
            lblSummary.MaximumSize = max;
            lblHint.MaximumSize = max;
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            if (!btnRetry.Enabled)
                return;

            SelectedAction = CompletionAction.RetryEligible;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnUndo_Click(object sender, EventArgs e)
        {
            if (!btnUndo.Enabled)
                return;

            SelectedAction = CompletionAction.Undo;
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
                MessageService.ShowInfo("KeeFetch could not copy the summary.", ex.Message);
            }
        }

        private void btnExportDiagnostics_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export KeeFetch diagnostics";
                dialog.Filter = "Text log (*.log)|*.log|Spreadsheet (*.csv)|*.csv";
                // No extension, so the chosen file type supplies it.
                dialog.FileName = "KeeFetch-diagnostics";
                dialog.DefaultExt = "log";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    result.Diagnostics.WriteTo(dialog.FileName);
                    lblHint.Text = "Diagnostics saved to " + dialog.FileName +
                        ". Entries are numbered and web addresses have no credentials or query strings.";
                }
                catch (Exception ex)
                {
                    Logger.Error("ExportDiagnostics", ex);
                    MessageService.ShowWarning("KeeFetch could not save the diagnostics.", ex.Message);
                }
            }
        }
    }
}
