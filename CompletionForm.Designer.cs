namespace KeeFetch
{
    partial class CompletionForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblExplanation;
        private System.Windows.Forms.TableLayoutPanel metadataLayout;
        private System.Windows.Forms.Label lblProfileCaption;
        private System.Windows.Forms.Label lblProfileValue;
        private System.Windows.Forms.Label lblElapsedCaption;
        private System.Windows.Forms.Label lblElapsedValue;
        private System.Windows.Forms.GroupBox grpCounts;
        private System.Windows.Forms.TableLayoutPanel countsLayout;
        private System.Windows.Forms.Label lblTotalCaption;
        private System.Windows.Forms.Label lblTotalValue;
        private System.Windows.Forms.Label lblUpdatedCaption;
        private System.Windows.Forms.Label lblUpdatedValue;
        private System.Windows.Forms.Label lblSkippedCaption;
        private System.Windows.Forms.Label lblSkippedValue;
        private System.Windows.Forms.Label lblNotFoundCaption;
        private System.Windows.Forms.Label lblNotFoundValue;
        private System.Windows.Forms.Label lblErrorsCaption;
        private System.Windows.Forms.Label lblErrorsValue;
        private System.Windows.Forms.Label lblCancelledCaption;
        private System.Windows.Forms.Label lblCancelledValue;
        private System.Windows.Forms.Label lblRetryHint;
        private System.Windows.Forms.Label lblDiagnostics;
        private System.Windows.Forms.FlowLayoutPanel actionPanel;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.Button btnCopySummary;
        private System.Windows.Forms.Button btnOpenDiagnostics;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new System.Windows.Forms.TableLayoutPanel();
            lblTitle = new System.Windows.Forms.Label();
            lblExplanation = new System.Windows.Forms.Label();
            metadataLayout = new System.Windows.Forms.TableLayoutPanel();
            lblProfileCaption = new System.Windows.Forms.Label();
            lblProfileValue = new System.Windows.Forms.Label();
            lblElapsedCaption = new System.Windows.Forms.Label();
            lblElapsedValue = new System.Windows.Forms.Label();
            grpCounts = new System.Windows.Forms.GroupBox();
            countsLayout = new System.Windows.Forms.TableLayoutPanel();
            lblTotalCaption = new System.Windows.Forms.Label();
            lblTotalValue = new System.Windows.Forms.Label();
            lblUpdatedCaption = new System.Windows.Forms.Label();
            lblUpdatedValue = new System.Windows.Forms.Label();
            lblSkippedCaption = new System.Windows.Forms.Label();
            lblSkippedValue = new System.Windows.Forms.Label();
            lblNotFoundCaption = new System.Windows.Forms.Label();
            lblNotFoundValue = new System.Windows.Forms.Label();
            lblErrorsCaption = new System.Windows.Forms.Label();
            lblErrorsValue = new System.Windows.Forms.Label();
            lblCancelledCaption = new System.Windows.Forms.Label();
            lblCancelledValue = new System.Windows.Forms.Label();
            lblRetryHint = new System.Windows.Forms.Label();
            lblDiagnostics = new System.Windows.Forms.Label();
            actionPanel = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
            btnRetry = new System.Windows.Forms.Button();
            btnCopySummary = new System.Windows.Forms.Button();
            btnOpenDiagnostics = new System.Windows.Forms.Button();
            rootLayout.SuspendLayout();
            metadataLayout.SuspendLayout();
            grpCounts.SuspendLayout();
            countsLayout.SuspendLayout();
            actionPanel.SuspendLayout();
            SuspendLayout();

            rootLayout.Name = "rootLayout";
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 7;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.Padding = new System.Windows.Forms.Padding(16);

            lblTitle.Name = "lblTitle";
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MessageBoxFont.FontFamily,
                12F,
                System.Drawing.FontStyle.Bold);
            lblTitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lblTitle.Text = "KeeFetch batch complete";

            lblExplanation.Name = "lblExplanation";
            lblExplanation.AccessibleName = "Batch completion explanation";
            lblExplanation.AutoSize = true;
            lblExplanation.Dock = System.Windows.Forms.DockStyle.Fill;
            lblExplanation.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);

            metadataLayout.Name = "metadataLayout";
            metadataLayout.AutoSize = true;
            metadataLayout.ColumnCount = 2;
            metadataLayout.RowCount = 2;
            metadataLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.AutoSize));
            metadataLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            metadataLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            metadataLayout.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);

            ConfigureCaption(lblProfileCaption, "lblProfileCaption", "Profile:", 0);
            ConfigureValue(lblProfileValue, "lblProfileValue", "Batch profile", 0);
            ConfigureCaption(lblElapsedCaption, "lblElapsedCaption", "Elapsed:", 1);
            ConfigureValue(lblElapsedValue, "lblElapsedValue", "Batch elapsed time", 1);
            metadataLayout.Controls.Add(lblProfileCaption, 0, 0);
            metadataLayout.Controls.Add(lblProfileValue, 1, 0);
            metadataLayout.Controls.Add(lblElapsedCaption, 0, 1);
            metadataLayout.Controls.Add(lblElapsedValue, 1, 1);

            grpCounts.Name = "grpCounts";
            grpCounts.AccessibleName = "Batch outcome counts";
            grpCounts.Dock = System.Windows.Forms.DockStyle.Fill;
            grpCounts.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
            grpCounts.Padding = new System.Windows.Forms.Padding(10);
            grpCounts.Text = "Outcomes";

            countsLayout.Name = "countsLayout";
            countsLayout.ColumnCount = 4;
            countsLayout.RowCount = 3;
            countsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 35F));
            countsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 15F));
            countsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 35F));
            countsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 15F));
            countsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 33.33F));
            countsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 33.33F));
            countsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 33.34F));
            countsLayout.Dock = System.Windows.Forms.DockStyle.Fill;

            ConfigureCountPair(lblTotalCaption, lblTotalValue,
                "lblTotalCaption", "Total", "lblTotalValue", "Total entries");
            ConfigureCountPair(lblUpdatedCaption, lblUpdatedValue,
                "lblUpdatedCaption", "Updated", "lblUpdatedValue", "Updated entries");
            ConfigureCountPair(lblSkippedCaption, lblSkippedValue,
                "lblSkippedCaption", "Skipped", "lblSkippedValue", "Skipped entries");
            ConfigureCountPair(lblNotFoundCaption, lblNotFoundValue,
                "lblNotFoundCaption", "Not found", "lblNotFoundValue", "Not found entries");
            ConfigureCountPair(lblErrorsCaption, lblErrorsValue,
                "lblErrorsCaption", "Errors", "lblErrorsValue", "Error entries");
            ConfigureCountPair(lblCancelledCaption, lblCancelledValue,
                "lblCancelledCaption", "Cancelled", "lblCancelledValue", "Cancelled entries");
            countsLayout.Controls.Add(lblTotalCaption, 0, 0);
            countsLayout.Controls.Add(lblTotalValue, 1, 0);
            countsLayout.Controls.Add(lblUpdatedCaption, 2, 0);
            countsLayout.Controls.Add(lblUpdatedValue, 3, 0);
            countsLayout.Controls.Add(lblSkippedCaption, 0, 1);
            countsLayout.Controls.Add(lblSkippedValue, 1, 1);
            countsLayout.Controls.Add(lblNotFoundCaption, 2, 1);
            countsLayout.Controls.Add(lblNotFoundValue, 3, 1);
            countsLayout.Controls.Add(lblErrorsCaption, 0, 2);
            countsLayout.Controls.Add(lblErrorsValue, 1, 2);
            countsLayout.Controls.Add(lblCancelledCaption, 2, 2);
            countsLayout.Controls.Add(lblCancelledValue, 3, 2);
            grpCounts.Controls.Add(countsLayout);

            lblRetryHint.Name = "lblRetryHint";
            lblRetryHint.AccessibleName = "Retry availability";
            lblRetryHint.AutoSize = true;
            lblRetryHint.Dock = System.Windows.Forms.DockStyle.Fill;
            lblRetryHint.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);

            lblDiagnostics.Name = "lblDiagnostics";
            lblDiagnostics.AccessibleName = "Diagnostics path";
            lblDiagnostics.AutoEllipsis = true;
            lblDiagnostics.AutoSize = false;
            lblDiagnostics.Dock = System.Windows.Forms.DockStyle.Fill;
            lblDiagnostics.Height = 28;
            lblDiagnostics.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);

            actionPanel.Name = "actionPanel";
            actionPanel.AutoSize = true;
            actionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            actionPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actionPanel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            actionPanel.WrapContents = true;

            ConfigureButton(btnClose, "btnClose", "&Close", "Close batch summary", 0);
            btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnClose.Click += btnClose_Click;
            ConfigureButton(btnRetry, "btnRetry", "&Retry Eligible Entries",
                "Retry eligible entries once", 1);
            btnRetry.Click += btnRetry_Click;
            ConfigureButton(btnCopySummary, "btnCopySummary", "Copy &Summary",
                "Copy batch summary", 2);
            btnCopySummary.Click += btnCopySummary_Click;
            ConfigureButton(btnOpenDiagnostics, "btnOpenDiagnostics", "&Open Diagnostics",
                "Open diagnostics file", 3);
            btnOpenDiagnostics.Click += btnOpenDiagnostics_Click;
            actionPanel.Controls.Add(btnClose);
            actionPanel.Controls.Add(btnRetry);
            actionPanel.Controls.Add(btnCopySummary);
            actionPanel.Controls.Add(btnOpenDiagnostics);

            rootLayout.Controls.Add(lblTitle, 0, 0);
            rootLayout.Controls.Add(lblExplanation, 0, 1);
            rootLayout.Controls.Add(metadataLayout, 0, 2);
            rootLayout.Controls.Add(grpCounts, 0, 3);
            rootLayout.Controls.Add(lblRetryHint, 0, 4);
            rootLayout.Controls.Add(lblDiagnostics, 0, 5);
            rootLayout.Controls.Add(actionPanel, 0, 6);

            AcceptButton = btnClose;
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            CancelButton = btnClose;
            ClientSize = new System.Drawing.Size(650, 500);
            Controls.Add(rootLayout);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(600, 480);
            Name = "CompletionForm";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "KeeFetch Results";

            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            metadataLayout.ResumeLayout(false);
            metadataLayout.PerformLayout();
            grpCounts.ResumeLayout(false);
            countsLayout.ResumeLayout(false);
            countsLayout.PerformLayout();
            actionPanel.ResumeLayout(false);
            actionPanel.PerformLayout();
            ResumeLayout(false);
        }

        private static void ConfigureCaption(System.Windows.Forms.Label label,
            string name, string text, int row)
        {
            label.Name = name;
            label.AutoSize = true;
            label.Margin = new System.Windows.Forms.Padding(0, row == 0 ? 0 : 4, 12, 0);
            label.Text = text;
        }

        private static void ConfigureValue(System.Windows.Forms.Label label,
            string name, string accessibleName, int row)
        {
            label.Name = name;
            label.AccessibleName = accessibleName;
            label.AutoSize = true;
            label.Margin = new System.Windows.Forms.Padding(0, row == 0 ? 0 : 4, 0, 0);
        }

        private static void ConfigureCountPair(System.Windows.Forms.Label caption,
            System.Windows.Forms.Label value, string captionName, string captionText,
            string valueName, string accessibleName)
        {
            caption.Name = captionName;
            caption.AutoSize = true;
            caption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            caption.Text = captionText + ":";
            value.Name = valueName;
            value.AccessibleName = accessibleName;
            value.AutoSize = true;
            value.Anchor = System.Windows.Forms.AnchorStyles.Left;
            value.Text = "0";
        }

        private static void ConfigureButton(System.Windows.Forms.Button button,
            string name, string text, string accessibleName, int tabIndex)
        {
            button.Name = name;
            button.AccessibleName = accessibleName;
            button.AutoSize = true;
            button.TabIndex = tabIndex;
            button.Text = text;
            button.UseVisualStyleBackColor = true;
        }
    }
}
