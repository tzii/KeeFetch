namespace KeeFetch
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.CheckBox chkPrefixUrls;
        private System.Windows.Forms.CheckBox chkUseTitleField;
        private System.Windows.Forms.CheckBox chkSkipExistingIcons;
        private System.Windows.Forms.CheckBox chkAutoSave;
        private System.Windows.Forms.CheckBox chkAllowSelfSigned;
        private System.Windows.Forms.CheckBox chkUseThirdPartyFallbacks;
        private System.Windows.Forms.NumericUpDown numMaxIconSize;
        private System.Windows.Forms.NumericUpDown numTimeout;
        private System.Windows.Forms.TextBox txtIconPrefix;
        private System.Windows.Forms.Label lblMaxIconSize;
        private System.Windows.Forms.Label lblTimeout;
        private System.Windows.Forms.Label lblIconPrefix;
        private System.Windows.Forms.Label lblTimeoutUnit;
        private System.Windows.Forms.Label lblIconSizeUnit;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox grpDownload;
        private System.Windows.Forms.GroupBox grpIcons;
        private System.Windows.Forms.GroupBox grpNetwork;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.chkPrefixUrls = new System.Windows.Forms.CheckBox();
            this.chkUseTitleField = new System.Windows.Forms.CheckBox();
            this.chkSkipExistingIcons = new System.Windows.Forms.CheckBox();
            this.chkAutoSave = new System.Windows.Forms.CheckBox();
            this.chkAllowSelfSigned = new System.Windows.Forms.CheckBox();
            this.chkUseThirdPartyFallbacks = new System.Windows.Forms.CheckBox();
            this.numMaxIconSize = new System.Windows.Forms.NumericUpDown();
            this.numTimeout = new System.Windows.Forms.NumericUpDown();
            this.txtIconPrefix = new System.Windows.Forms.TextBox();
            this.lblMaxIconSize = new System.Windows.Forms.Label();
            this.lblTimeout = new System.Windows.Forms.Label();
            this.lblIconPrefix = new System.Windows.Forms.Label();
            this.lblTimeoutUnit = new System.Windows.Forms.Label();
            this.lblIconSizeUnit = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpDownload = new System.Windows.Forms.GroupBox();
            this.grpIcons = new System.Windows.Forms.GroupBox();
            this.grpNetwork = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.numMaxIconSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).BeginInit();
            this.grpDownload.SuspendLayout();
            this.grpIcons.SuspendLayout();
            this.grpNetwork.SuspendLayout();
            this.SuspendLayout();
            //
            // grpDownload
            //
            this.grpDownload.Text = "Download Options";
            this.grpDownload.Location = new System.Drawing.Point(12, 12);
            this.grpDownload.Size = new System.Drawing.Size(410, 110);
            this.grpDownload.Controls.Add(this.chkPrefixUrls);
            this.grpDownload.Controls.Add(this.chkUseTitleField);
            this.grpDownload.Controls.Add(this.chkAutoSave);
            //
            // chkPrefixUrls
            //
            this.chkPrefixUrls.Text = "Automatically prefix URLs with https://";
            this.chkPrefixUrls.Location = new System.Drawing.Point(15, 25);
            this.chkPrefixUrls.Size = new System.Drawing.Size(380, 22);
            this.chkPrefixUrls.AutoSize = true;
            //
            // chkUseTitleField
            //
            this.chkUseTitleField.Text = "Use Title field if URL field is empty";
            this.chkUseTitleField.Location = new System.Drawing.Point(15, 50);
            this.chkUseTitleField.Size = new System.Drawing.Size(380, 22);
            this.chkUseTitleField.AutoSize = true;
            //
            // chkAutoSave
            //
            this.chkAutoSave.Text = "Automatically save database after downloading";
            this.chkAutoSave.Location = new System.Drawing.Point(15, 75);
            this.chkAutoSave.Size = new System.Drawing.Size(380, 22);
            this.chkAutoSave.AutoSize = true;
            //
            // grpIcons
            //
            this.grpIcons.Text = "Icon Options";
            this.grpIcons.Location = new System.Drawing.Point(12, 130);
            this.grpIcons.Size = new System.Drawing.Size(410, 130);
            this.grpIcons.Controls.Add(this.chkSkipExistingIcons);
            this.grpIcons.Controls.Add(this.lblMaxIconSize);
            this.grpIcons.Controls.Add(this.numMaxIconSize);
            this.grpIcons.Controls.Add(this.lblIconSizeUnit);
            this.grpIcons.Controls.Add(this.lblIconPrefix);
            this.grpIcons.Controls.Add(this.txtIconPrefix);
            //
            // chkSkipExistingIcons
            //
            this.chkSkipExistingIcons.Text = "Skip entries that already have custom icons";
            this.chkSkipExistingIcons.Location = new System.Drawing.Point(15, 25);
            this.chkSkipExistingIcons.Size = new System.Drawing.Size(380, 22);
            this.chkSkipExistingIcons.AutoSize = true;
            //
            // lblMaxIconSize
            //
            this.lblMaxIconSize.Text = "Maximum icon size:";
            this.lblMaxIconSize.Location = new System.Drawing.Point(15, 55);
            this.lblMaxIconSize.Size = new System.Drawing.Size(120, 20);
            this.lblMaxIconSize.AutoSize = true;
            //
            // numMaxIconSize
            //
            this.numMaxIconSize.Location = new System.Drawing.Point(145, 53);
            this.numMaxIconSize.Size = new System.Drawing.Size(70, 23);
            this.numMaxIconSize.Minimum = 16;
            this.numMaxIconSize.Maximum = 256;
            this.numMaxIconSize.Value = 128;
            this.numMaxIconSize.Increment = 16;
            //
            // lblIconSizeUnit
            //
            this.lblIconSizeUnit.Text = "px";
            this.lblIconSizeUnit.Location = new System.Drawing.Point(220, 55);
            this.lblIconSizeUnit.Size = new System.Drawing.Size(30, 20);
            this.lblIconSizeUnit.AutoSize = true;
            //
            // lblIconPrefix
            //
            this.lblIconPrefix.Text = "Icon name prefix:";
            this.lblIconPrefix.Location = new System.Drawing.Point(15, 90);
            this.lblIconPrefix.Size = new System.Drawing.Size(120, 20);
            this.lblIconPrefix.AutoSize = true;
            //
            // txtIconPrefix
            //
            this.txtIconPrefix.Location = new System.Drawing.Point(145, 88);
            this.txtIconPrefix.Size = new System.Drawing.Size(150, 23);
            //
            // grpNetwork
            //
            this.grpNetwork.Text = "Network Options";
            this.grpNetwork.Location = new System.Drawing.Point(12, 268);
            this.grpNetwork.Size = new System.Drawing.Size(410, 130);
            this.grpNetwork.Controls.Add(this.chkAllowSelfSigned);
            this.grpNetwork.Controls.Add(this.lblTimeout);
            this.grpNetwork.Controls.Add(this.numTimeout);
            this.grpNetwork.Controls.Add(this.lblTimeoutUnit);
            this.grpNetwork.Controls.Add(this.chkUseThirdPartyFallbacks);
            //
            // chkAllowSelfSigned
            //
            this.chkAllowSelfSigned.Text = "Allow self-signed SSL certificates";
            this.chkAllowSelfSigned.Location = new System.Drawing.Point(15, 25);
            this.chkAllowSelfSigned.Size = new System.Drawing.Size(380, 22);
            this.chkAllowSelfSigned.AutoSize = true;
            //
            // lblTimeout
            //
            this.lblTimeout.Text = "Connection timeout:";
            this.lblTimeout.Location = new System.Drawing.Point(15, 58);
            this.lblTimeout.Size = new System.Drawing.Size(120, 20);
            this.lblTimeout.AutoSize = true;
            //
            // numTimeout
            //
            this.numTimeout.Location = new System.Drawing.Point(145, 56);
            this.numTimeout.Size = new System.Drawing.Size(70, 23);
            this.numTimeout.Minimum = 5;
            this.numTimeout.Maximum = 60;
            this.numTimeout.Value = 15;
            //
            // lblTimeoutUnit
            //
            this.lblTimeoutUnit.Text = "seconds";
            this.lblTimeoutUnit.Location = new System.Drawing.Point(220, 58);
            this.lblTimeoutUnit.Size = new System.Drawing.Size(60, 20);
            this.lblTimeoutUnit.AutoSize = true;
            //
            // chkUseThirdPartyFallbacks
            //
            this.chkUseThirdPartyFallbacks.Text = "Use third-party favicon services (Google, DuckDuckGo, etc.)";
            this.chkUseThirdPartyFallbacks.Location = new System.Drawing.Point(15, 88);
            this.chkUseThirdPartyFallbacks.Size = new System.Drawing.Size(380, 22);
            this.chkUseThirdPartyFallbacks.AutoSize = true;
            //
            // btnOK
            //
            this.btnOK.Text = "OK";
            this.btnOK.Location = new System.Drawing.Point(266, 410);
            this.btnOK.Size = new System.Drawing.Size(75, 28);
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel
            //
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new System.Drawing.Point(347, 410);
            this.btnCancel.Size = new System.Drawing.Size(75, 28);
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // SettingsForm
            //
            this.Text = "KeeFetch Settings";
            this.ClientSize = new System.Drawing.Size(434, 450);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.grpDownload);
            this.Controls.Add(this.grpIcons);
            this.Controls.Add(this.grpNetwork);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            ((System.ComponentModel.ISupportInitialize)(this.numMaxIconSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).EndInit();
            this.grpDownload.ResumeLayout(false);
            this.grpDownload.PerformLayout();
            this.grpIcons.ResumeLayout(false);
            this.grpIcons.PerformLayout();
            this.grpNetwork.ResumeLayout(false);
            this.grpNetwork.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
