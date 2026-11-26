namespace KeeFetch
{
    partial class CompletionForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.ListView lstOutcomes;
        private System.Windows.Forms.ColumnHeader colEntry;
        private System.Windows.Forms.ColumnHeader colResult;
        private System.Windows.Forms.ColumnHeader colSource;
        private System.Windows.Forms.ColumnHeader colDetails;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.TableLayoutPanel buttonLayout;
        private System.Windows.Forms.FlowLayoutPanel secondaryActions;
        private System.Windows.Forms.FlowLayoutPanel primaryActions;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.Button btnUndo;
        private System.Windows.Forms.Button btnExportDiagnostics;
        private System.Windows.Forms.Button btnCopySummary;

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
            lblSummary = new System.Windows.Forms.Label();
            lstOutcomes = new System.Windows.Forms.ListView();
            colEntry = new System.Windows.Forms.ColumnHeader();
            colResult = new System.Windows.Forms.ColumnHeader();
            colSource = new System.Windows.Forms.ColumnHeader();
            colDetails = new System.Windows.Forms.ColumnHeader();
            lblHint = new System.Windows.Forms.Label();
            buttonLayout = new System.Windows.Forms.TableLayoutPanel();
            secondaryActions = new System.Windows.Forms.FlowLayoutPanel();
            primaryActions = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
            btnRetry = new System.Windows.Forms.Button();
            btnUndo = new System.Windows.Forms.Button();
            btnExportDiagnostics = new System.Windows.Forms.Button();
            btnCopySummary = new System.Windows.Forms.Button();
            rootLayout.SuspendLayout();
            buttonLayout.SuspendLayout();
            secondaryActions.SuspendLayout();
            primaryActions.SuspendLayout();
            SuspendLayout();

            rootLayout.Name = "rootLayout";
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 5;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
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
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.Padding = new System.Windows.Forms.Padding(16);

            lblTitle.Name = "lblTitle";
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MessageBoxFont.FontFamily,
                12F,
                System.Drawing.FontStyle.Bold);
            lblTitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);

            lblSummary.Name = "lblSummary";
            lblSummary.AccessibleDescription = "Download summary";
            lblSummary.AutoSize = true;
            lblSummary.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);

            colEntry.Text = "Entry";
            colEntry.Width = 170;
            colResult.Text = "Result";
            colResult.Width = 110;
            colSource.Text = "Icon from";
            colSource.Width = 100;
            colDetails.Text = "Details";
            colDetails.Width = 260;

            lstOutcomes.Name = "lstOutcomes";
            lstOutcomes.AccessibleName = "Result for each entry";
            lstOutcomes.Columns.AddRange(new[] { colEntry, colResult, colSource, colDetails });
            lstOutcomes.Dock = System.Windows.Forms.DockStyle.Fill;
            lstOutcomes.FullRowSelect = true;
            lstOutcomes.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            lstOutcomes.HideSelection = false;
            lstOutcomes.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lstOutcomes.MultiSelect = false;
            lstOutcomes.ShowItemToolTips = true;
            lstOutcomes.TabIndex = 0;
            lstOutcomes.UseCompatibleStateImageBehavior = false;
            lstOutcomes.View = System.Windows.Forms.View.Details;

            lblHint.Name = "lblHint";
            lblHint.AccessibleDescription = "What you can do next";
            lblHint.AutoSize = true;
            lblHint.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);

            buttonLayout.Name = "buttonLayout";
            buttonLayout.AutoSize = true;
            buttonLayout.ColumnCount = 2;
            buttonLayout.RowCount = 2;
            buttonLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.AutoSize));
            buttonLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            buttonLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            buttonLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            buttonLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            buttonLayout.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            buttonLayout.TabIndex = 1;

            secondaryActions.Name = "secondaryActions";
            secondaryActions.AutoSize = true;
            secondaryActions.Dock = System.Windows.Forms.DockStyle.Fill;
            secondaryActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            secondaryActions.Margin = new System.Windows.Forms.Padding(0);
            secondaryActions.TabIndex = 0;
            secondaryActions.WrapContents = false;

            primaryActions.Name = "primaryActions";
            primaryActions.AutoSize = true;
            primaryActions.Dock = System.Windows.Forms.DockStyle.Fill;
            primaryActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            primaryActions.Margin = new System.Windows.Forms.Padding(0);
            primaryActions.TabIndex = 1;
            primaryActions.WrapContents = false;

            ConfigureButton(btnClose, "btnClose", "&Close", "Close the download summary", 2);
            btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnClose.Click += btnClose_Click;
            ConfigureButton(btnUndo, "btnUndo", "&Undo this run",
                "Restore the icons the entries had before this run", 1);
            btnUndo.Click += btnUndo_Click;
            ConfigureButton(btnRetry, "btnRetry", "&Retry",
                "Retry entries that failed or had no icon", 0);
            btnRetry.Click += btnRetry_Click;
            ConfigureButton(btnExportDiagnostics, "btnExportDiagnostics", "&Export diagnostics...",
                "Export diagnostics without entry titles or credentials", 0);
            btnExportDiagnostics.Click += btnExportDiagnostics_Click;
            ConfigureButton(btnCopySummary, "btnCopySummary", "Copy &summary",
                "Copy the download summary", 1);
            btnCopySummary.Click += btnCopySummary_Click;

            primaryActions.Controls.Add(btnClose);
            primaryActions.Controls.Add(btnUndo);
            primaryActions.Controls.Add(btnRetry);
            secondaryActions.Controls.Add(btnExportDiagnostics);
            secondaryActions.Controls.Add(btnCopySummary);
            buttonLayout.Controls.Add(secondaryActions, 0, 0);
            buttonLayout.Controls.Add(primaryActions, 1, 0);

            rootLayout.Controls.Add(lblTitle, 0, 0);
            rootLayout.Controls.Add(lblSummary, 0, 1);
            rootLayout.Controls.Add(lstOutcomes, 0, 2);
            rootLayout.Controls.Add(lblHint, 0, 3);
            rootLayout.Controls.Add(buttonLayout, 0, 4);

            AcceptButton = btnClose;
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            CancelButton = btnClose;
            ClientSize = new System.Drawing.Size(720, 460);
            Controls.Add(rootLayout);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(660, 400);
            Name = "CompletionForm";
            ShowIcon = true;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "KeeFetch";

            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            buttonLayout.ResumeLayout(false);
            buttonLayout.PerformLayout();
            secondaryActions.ResumeLayout(false);
            secondaryActions.PerformLayout();
            primaryActions.ResumeLayout(false);
            primaryActions.PerformLayout();
            ResumeLayout(false);
        }

        private static void ConfigureButton(System.Windows.Forms.Button button,
            string name, string text, string description, int tabIndex)
        {
            // The accessible name repeats the visible text so voice control
            // finds the button by what it shows; the description explains it.
            button.Name = name;
            button.AccessibleName = text.Replace("&", string.Empty);
            button.AccessibleDescription = description;
            button.AutoSize = true;
            button.MinimumSize = new System.Drawing.Size(88, 0);
            button.TabIndex = tabIndex;
            button.Text = text;
            button.UseVisualStyleBackColor = true;
        }
    }
}
