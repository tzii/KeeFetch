namespace KeeFetch
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Label lblValidationSummary;
        private System.Windows.Forms.TabControl tabSettings;
        private System.Windows.Forms.TabPage tabOverview;
        private System.Windows.Forms.TabPage tabDownloads;
        private System.Windows.Forms.TabPage tabProviders;
        private System.Windows.Forms.TabPage tabAdvanced;
        private KeeFetch.Settings.OverviewSettingsPage overviewPage;
        private KeeFetch.Settings.DownloadSettingsPage downloadPage;
        private KeeFetch.Settings.ProviderSettingsPage providerPage;
        private KeeFetch.Settings.AdvancedSettingsPage advancedPage;
        private System.Windows.Forms.FlowLayoutPanel actionPanel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ErrorProvider errorProvider;

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
            lblValidationSummary = new System.Windows.Forms.Label();
            tabSettings = new System.Windows.Forms.TabControl();
            tabOverview = new System.Windows.Forms.TabPage();
            tabDownloads = new System.Windows.Forms.TabPage();
            tabProviders = new System.Windows.Forms.TabPage();
            tabAdvanced = new System.Windows.Forms.TabPage();
            overviewPage = new KeeFetch.Settings.OverviewSettingsPage();
            downloadPage = new KeeFetch.Settings.DownloadSettingsPage();
            providerPage = new KeeFetch.Settings.ProviderSettingsPage();
            advancedPage = new KeeFetch.Settings.AdvancedSettingsPage();
            actionPanel = new System.Windows.Forms.FlowLayoutPanel();
            btnSave = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            errorProvider = new System.Windows.Forms.ErrorProvider(components);
            rootLayout.SuspendLayout();
            tabSettings.SuspendLayout();
            tabOverview.SuspendLayout();
            tabDownloads.SuspendLayout();
            tabProviders.SuspendLayout();
            tabAdvanced.SuspendLayout();
            actionPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)errorProvider).BeginInit();
            SuspendLayout();

            rootLayout.Name = "rootLayout";
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 3;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.Padding = new System.Windows.Forms.Padding(12);

            lblValidationSummary.Name = "lblValidationSummary";
            lblValidationSummary.AccessibleName = "Settings validation summary";
            lblValidationSummary.AutoSize = true;
            lblValidationSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            lblValidationSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            lblValidationSummary.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lblValidationSummary.Padding = new System.Windows.Forms.Padding(8);
            lblValidationSummary.Visible = false;

            tabSettings.Name = "tabSettings";
            tabSettings.AccessibleName = "KeeFetch settings pages";
            tabSettings.Controls.Add(tabOverview);
            tabSettings.Controls.Add(tabDownloads);
            tabSettings.Controls.Add(tabProviders);
            tabSettings.Controls.Add(tabAdvanced);
            tabSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            tabSettings.Margin = new System.Windows.Forms.Padding(0);
            tabSettings.SelectedIndex = 0;
            tabSettings.TabIndex = 0;

            ConfigureTabPage(tabOverview, "tabOverview", "Overview", overviewPage);
            ConfigureTabPage(tabDownloads, "tabDownloads", "Downloads", downloadPage);
            ConfigureTabPage(tabProviders, "tabProviders", "Providers", providerPage);
            ConfigureTabPage(tabAdvanced, "tabAdvanced", "Advanced", advancedPage);

            actionPanel.Name = "actionPanel";
            actionPanel.AutoSize = true;
            actionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            actionPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actionPanel.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            actionPanel.WrapContents = false;

            btnCancel.Name = "btnCancel";
            btnCancel.AccessibleName = "Cancel settings changes";
            btnCancel.AutoSize = true;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            btnSave.Name = "btnSave";
            btnSave.AccessibleName = "Save KeeFetch settings";
            btnSave.AutoSize = true;
            btnSave.TabIndex = 0;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;

            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnSave);
            rootLayout.Controls.Add(lblValidationSummary, 0, 0);
            rootLayout.Controls.Add(tabSettings, 0, 1);
            rootLayout.Controls.Add(actionPanel, 0, 2);

            errorProvider.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;
            errorProvider.ContainerControl = this;

            AcceptButton = btnSave;
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(760, 560);
            Controls.Add(rootLayout);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(640, 480);
            Name = "SettingsForm";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "KeeFetch Settings";

            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            tabSettings.ResumeLayout(false);
            tabOverview.ResumeLayout(false);
            tabDownloads.ResumeLayout(false);
            tabProviders.ResumeLayout(false);
            tabAdvanced.ResumeLayout(false);
            actionPanel.ResumeLayout(false);
            actionPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)errorProvider).EndInit();
            ResumeLayout(false);
        }

        private static void ConfigureTabPage(System.Windows.Forms.TabPage tabPage,
            string name, string text, System.Windows.Forms.Control content)
        {
            tabPage.Name = name;
            tabPage.Padding = new System.Windows.Forms.Padding(3);
            tabPage.Text = text;
            tabPage.UseVisualStyleBackColor = true;
            content.Dock = System.Windows.Forms.DockStyle.Fill;
            tabPage.Controls.Add(content);
        }
    }
}
