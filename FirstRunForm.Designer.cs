namespace KeeFetch
{
    partial class FirstRunForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.Label lblProfilePrompt;
        private System.Windows.Forms.ListBox lstProfiles;
        private System.Windows.Forms.Label lblProfileDescription;
        private System.Windows.Forms.GroupBox grpPrivacy;
        private System.Windows.Forms.TableLayoutPanel privacyLayout;
        private System.Windows.Forms.Label lblPrivacyDisclosure;
        private System.Windows.Forms.LinkLabel lnkPrivacy;
        private System.Windows.Forms.FlowLayoutPanel actionPanel;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;

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
            lblIntro = new System.Windows.Forms.Label();
            lblProfilePrompt = new System.Windows.Forms.Label();
            lstProfiles = new System.Windows.Forms.ListBox();
            lblProfileDescription = new System.Windows.Forms.Label();
            grpPrivacy = new System.Windows.Forms.GroupBox();
            privacyLayout = new System.Windows.Forms.TableLayoutPanel();
            lblPrivacyDisclosure = new System.Windows.Forms.Label();
            lnkPrivacy = new System.Windows.Forms.LinkLabel();
            actionPanel = new System.Windows.Forms.FlowLayoutPanel();
            btnConfirm = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            rootLayout.SuspendLayout();
            grpPrivacy.SuspendLayout();
            privacyLayout.SuspendLayout();
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
                System.Windows.Forms.SizeType.Absolute, 46F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Absolute, 100F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Absolute, 82F));
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
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
            lblTitle.Text = "Choose how KeeFetch finds icons";

            lblIntro.Name = "lblIntro";
            lblIntro.AutoSize = false;
            lblIntro.Dock = System.Windows.Forms.DockStyle.Fill;
            lblIntro.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lblIntro.Text = "Choose a profile before the first download. You can change this later in KeeFetch Settings.";

            lblProfilePrompt.Name = "lblProfilePrompt";
            lblProfilePrompt.AutoSize = true;
            lblProfilePrompt.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            lblProfilePrompt.Text = "Fetch profile:";

            lstProfiles.Name = "lstProfiles";
            lstProfiles.AccessibleName = "First-run fetch profile";
            lstProfiles.Dock = System.Windows.Forms.DockStyle.Fill;
            lstProfiles.IntegralHeight = false;
            lstProfiles.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lstProfiles.TabIndex = 0;
            lstProfiles.SelectedIndexChanged += lstProfiles_SelectedIndexChanged;

            lblProfileDescription.Name = "lblProfileDescription";
            lblProfileDescription.AccessibleName = "Selected profile description";
            lblProfileDescription.AutoSize = false;
            lblProfileDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            lblProfileDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            lblProfileDescription.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
            lblProfileDescription.Padding = new System.Windows.Forms.Padding(8);

            grpPrivacy.Name = "grpPrivacy";
            grpPrivacy.AccessibleName = "Privacy disclosure";
            grpPrivacy.Dock = System.Windows.Forms.DockStyle.Fill;
            grpPrivacy.Margin = new System.Windows.Forms.Padding(0);
            grpPrivacy.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            grpPrivacy.Text = "Before you continue";

            privacyLayout.Name = "privacyLayout";
            privacyLayout.ColumnCount = 1;
            privacyLayout.RowCount = 2;
            privacyLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            privacyLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            privacyLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            privacyLayout.Dock = System.Windows.Forms.DockStyle.Fill;

            lblPrivacyDisclosure.Name = "lblPrivacyDisclosure";
            lblPrivacyDisclosure.AccessibleName = "Third-party domain sharing disclosure";
            lblPrivacyDisclosure.AutoSize = false;
            lblPrivacyDisclosure.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPrivacyDisclosure.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            lblPrivacyDisclosure.Text = "Some profiles may send a site's domain name to third-party favicon services when direct fetching is not enough. The Privacy profile contacts only the site itself. KeeFetch does not send usernames, passwords, or other entry fields.";

            lnkPrivacy.Name = "lnkPrivacy";
            lnkPrivacy.AccessibleName = "Open KeeFetch privacy details";
            lnkPrivacy.AutoSize = true;
            lnkPrivacy.Margin = new System.Windows.Forms.Padding(0);
            lnkPrivacy.TabIndex = 1;
            lnkPrivacy.Text = "Read KeeFetch privacy details";
            lnkPrivacy.LinkClicked += lnkPrivacy_LinkClicked;

            privacyLayout.Controls.Add(lblPrivacyDisclosure, 0, 0);
            privacyLayout.Controls.Add(lnkPrivacy, 0, 1);
            grpPrivacy.Controls.Add(privacyLayout);

            actionPanel.Name = "actionPanel";
            actionPanel.AutoSize = true;
            actionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            actionPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actionPanel.Margin = new System.Windows.Forms.Padding(0, 12, 0, 0);
            actionPanel.WrapContents = false;

            btnCancel.Name = "btnCancel";
            btnCancel.AccessibleName = "Cancel first-run setup";
            btnCancel.AutoSize = true;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Ca&ncel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            btnConfirm.Name = "btnConfirm";
            btnConfirm.AccessibleName = "Confirm fetch profile and continue";
            btnConfirm.AutoSize = true;
            btnConfirm.TabIndex = 0;
            btnConfirm.Text = "&Confirm and Continue";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;

            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnConfirm);
            rootLayout.Controls.Add(lblTitle, 0, 0);
            rootLayout.Controls.Add(lblIntro, 0, 1);
            rootLayout.Controls.Add(lblProfilePrompt, 0, 2);
            rootLayout.Controls.Add(lstProfiles, 0, 3);
            rootLayout.Controls.Add(lblProfileDescription, 0, 4);
            rootLayout.Controls.Add(grpPrivacy, 0, 5);
            rootLayout.Controls.Add(actionPanel, 0, 6);

            AcceptButton = btnConfirm;
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(700, 520);
            Controls.Add(rootLayout);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(600, 540);
            Name = "FirstRunForm";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Welcome to KeeFetch";

            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            grpPrivacy.ResumeLayout(false);
            privacyLayout.ResumeLayout(false);
            privacyLayout.PerformLayout();
            actionPanel.ResumeLayout(false);
            actionPanel.PerformLayout();
            ResumeLayout(false);
        }
    }
}
