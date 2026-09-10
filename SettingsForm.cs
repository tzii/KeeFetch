using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using KeeFetch.Settings;

namespace KeeFetch
{
    public partial class SettingsForm : Form
    {
        private readonly Configuration config;
        private readonly SettingsDraft draft;
        private readonly Action persistOnSave;

        public SettingsForm(Configuration config)
            : this(config, null)
        {
        }

        internal SettingsForm(Configuration config, Action persistOnSave)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            this.config = config;
            this.persistOnSave = persistOnSave;
            draft = SettingsDraft.FromConfiguration(config);
            InitializeComponent();
            LoadPagesFromDraft();
            overviewPage.ProfileChanged += overviewPage_ProfileChanged;
        }

        private void LoadPagesFromDraft()
        {
            overviewPage.LoadFromDraft(draft);
            downloadPage.LoadFromDraft(draft);
            providerPage.LoadFromDraft(draft);
            advancedPage.LoadFromDraft(draft);
        }

        private void overviewPage_ProfileChanged(object sender, EventArgs e)
        {
            providerPage.LoadFromDraft(draft);
            advancedPage.LoadFromDraft(draft);
            ClearValidationErrors();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            overviewPage.SaveToDraft(draft);
            downloadPage.SaveToDraft(draft);
            providerPage.SaveToDraft(draft);
            advancedPage.SaveToDraft(draft);

            IReadOnlyList<SettingsValidationError> errors = draft.Validate();
            if (errors.Count > 0)
            {
                ShowValidationErrors(errors);
                return;
            }

            ClearValidationErrors();
            draft.ApplyTo(config);
            if (persistOnSave != null)
                persistOnSave();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ShowValidationErrors(IReadOnlyList<SettingsValidationError> errors)
        {
            ClearValidationErrors();
            if (errors == null || errors.Count == 0)
                return;

            lblValidationSummary.Text = "Please correct the following settings: " +
                string.Join(" ", errors.Select(error => error.Message).ToArray());
            lblValidationSummary.Visible = true;

            foreach (IGrouping<string, SettingsValidationError> group in
                errors.GroupBy(error => error.PageId, StringComparer.OrdinalIgnoreCase))
            {
                Control pageControl = GetPageControl(group.Key);
                if (pageControl != null)
                {
                    errorProvider.SetError(pageControl,
                        string.Join(" ", group.Select(error => error.Message).ToArray()));
                }
            }

            SettingsValidationError first = errors[0];
            TabPage page = GetTabPage(first.PageId);
            if (page != null)
                tabSettings.SelectedTab = page;
            FocusPageControl(first);
        }

        private void ClearValidationErrors()
        {
            errorProvider.Clear();
            lblValidationSummary.Text = string.Empty;
            lblValidationSummary.Visible = false;
        }

        private TabPage GetTabPage(string pageId)
        {
            if (string.Equals(pageId, "overview", StringComparison.OrdinalIgnoreCase))
                return tabOverview;
            if (string.Equals(pageId, "downloads", StringComparison.OrdinalIgnoreCase))
                return tabDownloads;
            if (string.Equals(pageId, "providers", StringComparison.OrdinalIgnoreCase))
                return tabProviders;
            if (string.Equals(pageId, "advanced", StringComparison.OrdinalIgnoreCase))
                return tabAdvanced;
            return null;
        }

        private Control GetPageControl(string pageId)
        {
            if (string.Equals(pageId, "overview", StringComparison.OrdinalIgnoreCase))
                return overviewPage;
            if (string.Equals(pageId, "downloads", StringComparison.OrdinalIgnoreCase))
                return downloadPage;
            if (string.Equals(pageId, "providers", StringComparison.OrdinalIgnoreCase))
                return providerPage;
            if (string.Equals(pageId, "advanced", StringComparison.OrdinalIgnoreCase))
                return advancedPage;
            return null;
        }

        private void FocusPageControl(SettingsValidationError error)
        {
            if (error == null)
                return;

            if (string.Equals(error.PageId, "overview", StringComparison.OrdinalIgnoreCase))
                overviewPage.FocusControl(error.ControlKey);
            else if (string.Equals(error.PageId, "downloads", StringComparison.OrdinalIgnoreCase))
                downloadPage.FocusControl(error.ControlKey);
            else if (string.Equals(error.PageId, "providers", StringComparison.OrdinalIgnoreCase))
                providerPage.FocusControl(error.ControlKey);
            else if (string.Equals(error.PageId, "advanced", StringComparison.OrdinalIgnoreCase))
                advancedPage.FocusControl(error.ControlKey);
        }
    }
}
