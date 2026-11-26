namespace KeeFetch
{
    partial class FirstRunForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.Label lblNotice;
        private System.Windows.Forms.TableLayoutPanel profileLayout;
        private System.Windows.Forms.Label lblPrivacyHeading;
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
            lblNotice = new System.Windows.Forms.Label();
            profileLayout = new System.Windows.Forms.TableLayoutPanel();
            lblPrivacyHeading = new System.Windows.Forms.Label();
            lblPrivacyDisclosure = new System.Windows.Forms.Label();
            lnkPrivacy = new System.Windows.Forms.LinkLabel();
            actionPanel = new System.Windows.Forms.FlowLayoutPanel();
            btnConfirm = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            rootLayout.SuspendLayout();
            actionPanel.SuspendLayout();
            SuspendLayout();

            rootLayout.Name = "rootLayout";
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 9;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++)
            {
                rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.AutoSize));
            }
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
            lblIntro.AutoSize = true;
            lblIntro.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            lblIntro.Text = "KeeFetch looks up an icon for each entry from its URL. " +
                "You can change this choice later in Tools > KeeFetch > Settings.";

            lblNotice.Name = "lblNotice";
            lblNotice.AccessibleDescription = "Profile change notice";
            lblNotice.AutoSize = true;
            lblNotice.Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MessageBoxFont,
                System.Drawing.FontStyle.Bold);
            lblNotice.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            lblNotice.Visible = false;

            profileLayout.Name = "profileLayout";
            profileLayout.AccessibleName = "Fetch profiles";
            profileLayout.AccessibleRole = System.Windows.Forms.AccessibleRole.Grouping;
            profileLayout.AutoSize = true;
            profileLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            profileLayout.ColumnCount = 1;
            profileLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100F));
            profileLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            profileLayout.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            profileLayout.TabIndex = 0;

            lblPrivacyHeading.Name = "lblPrivacyHeading";
            lblPrivacyHeading.AutoSize = true;
            lblPrivacyHeading.Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MessageBoxFont,
                System.Drawing.FontStyle.Bold);
            lblPrivacyHeading.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            lblPrivacyHeading.Text = "What KeeFetch sends";

            lblPrivacyDisclosure.Name = "lblPrivacyDisclosure";
            lblPrivacyDisclosure.AccessibleDescription = "Third-party domain sharing disclosure";
            lblPrivacyDisclosure.AutoSize = true;
            lblPrivacyDisclosure.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);

            lnkPrivacy.Name = "lnkPrivacy";
            lnkPrivacy.AccessibleName = "Open KeeFetch privacy details";
            lnkPrivacy.AutoSize = true;
            lnkPrivacy.Margin = new System.Windows.Forms.Padding(0);
            lnkPrivacy.TabIndex = 1;
            lnkPrivacy.Text = "Privacy details";
            lnkPrivacy.LinkClicked += lnkPrivacy_LinkClicked;

            actionPanel.Name = "actionPanel";
            actionPanel.AutoSize = true;
            actionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            actionPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            actionPanel.Margin = new System.Windows.Forms.Padding(0, 12, 0, 0);
            actionPanel.TabIndex = 2;
            actionPanel.WrapContents = false;

            btnCancel.Name = "btnCancel";
            btnCancel.AccessibleName = "Cancel first-run setup";
            btnCancel.AutoSize = true;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.MinimumSize = new System.Drawing.Size(88, 0);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            btnConfirm.Name = "btnConfirm";
            btnConfirm.AccessibleName = "Confirm fetch profile and continue";
            btnConfirm.AutoSize = true;
            btnConfirm.MinimumSize = new System.Drawing.Size(88, 0);
            btnConfirm.TabIndex = 0;
            btnConfirm.Text = "&Continue";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;

            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnConfirm);
            rootLayout.Controls.Add(lblTitle, 0, 0);
            rootLayout.Controls.Add(lblIntro, 0, 1);
            rootLayout.Controls.Add(lblNotice, 0, 2);
            rootLayout.Controls.Add(profileLayout, 0, 3);
            rootLayout.Controls.Add(lblPrivacyHeading, 0, 4);
            rootLayout.Controls.Add(lblPrivacyDisclosure, 0, 5);
            rootLayout.Controls.Add(lnkPrivacy, 0, 6);
            rootLayout.Controls.Add(actionPanel, 0, 8);

            AcceptButton = btnConfirm;
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(600, 460);
            Controls.Add(rootLayout);
            ForeColor = System.Drawing.SystemColors.ControlText;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(560, 480);
            Name = "FirstRunForm";
            ShowIcon = true;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Welcome to KeeFetch";

            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            actionPanel.ResumeLayout(false);
            actionPanel.PerformLayout();
            ResumeLayout(false);
        }
    }
}
