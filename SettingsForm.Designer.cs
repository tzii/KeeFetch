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
        private System.Windows.Forms.CheckBox chkAllowSyntheticFallbacks;

        private System.Windows.Forms.CheckBox chkProviderDirectSite;
        private System.Windows.Forms.CheckBox chkProviderTwentyIcons;
        private System.Windows.Forms.CheckBox chkProviderDuckDuckGo;
        private System.Windows.Forms.CheckBox chkProviderGoogle;
        private System.Windows.Forms.CheckBox chkProviderYandex;
        private System.Windows.Forms.CheckBox chkProviderFavicone;
        private System.Windows.Forms.CheckBox chkProviderIconHorse;

        private System.Windows.Forms.NumericUpDown numMaxIconSize;
        private System.Windows.Forms.NumericUpDown numTimeout;
        private System.Windows.Forms.TextBox txtIconPrefix;
        private System.Windows.Forms.ListBox lstProviderOrder;
        private System.Windows.Forms.ComboBox cmbFetchPreset;

        private System.Windows.Forms.Label lblFetchPreset;
        private System.Windows.Forms.Label lblFetchPresetDescription;
        private System.Windows.Forms.Label lblMaxIconSize;
        private System.Windows.Forms.Label lblTimeout;
        private System.Windows.Forms.Label lblIconPrefix;
        private System.Windows.Forms.Label lblTimeoutUnit;
        private System.Windows.Forms.Label lblIconSizeUnit;
        private System.Windows.Forms.Label lblProviderOrderHint;

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnProviderUp;
        private System.Windows.Forms.Button btnProviderDown;
        private System.Windows.Forms.Button btnProviderReset;
        private System.Windows.Forms.GroupBox grpPreset;
        private System.Windows.Forms.GroupBox grpDownload;
        private System.Windows.Forms.GroupBox grpIcons;
        private System.Windows.Forms.GroupBox grpNetwork;
        private System.Windows.Forms.GroupBox grpProviders;

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
            this.chkAllowSyntheticFallbacks = new System.Windows.Forms.CheckBox();
            this.chkProviderDirectSite = new System.Windows.Forms.CheckBox();
            this.chkProviderTwentyIcons = new System.Windows.Forms.CheckBox();
            this.chkProviderDuckDuckGo = new System.Windows.Forms.CheckBox();
            this.chkProviderGoogle = new System.Windows.Forms.CheckBox();
            this.chkProviderYandex = new System.Windows.Forms.CheckBox();
            this.chkProviderFavicone = new System.Windows.Forms.CheckBox();
            this.chkProviderIconHorse = new System.Windows.Forms.CheckBox();
            this.numMaxIconSize = new System.Windows.Forms.NumericUpDown();
            this.numTimeout = new System.Windows.Forms.NumericUpDown();
            this.txtIconPrefix = new System.Windows.Forms.TextBox();
            this.lstProviderOrder = new System.Windows.Forms.ListBox();
            this.cmbFetchPreset = new System.Windows.Forms.ComboBox();
            this.lblFetchPreset = new System.Windows.Forms.Label();
            this.lblFetchPresetDescription = new System.Windows.Forms.Label();
            this.lblMaxIconSize = new System.Windows.Forms.Label();
            this.lblTimeout = new System.Windows.Forms.Label();
            this.lblIconPrefix = new System.Windows.Forms.Label();
            this.lblTimeoutUnit = new System.Windows.Forms.Label();
            this.lblIconSizeUnit = new System.Windows.Forms.Label();
            this.lblProviderOrderHint = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnProviderUp = new System.Windows.Forms.Button();
            this.btnProviderDown = new System.Windows.Forms.Button();
            this.btnProviderReset = new System.Windows.Forms.Button();
            this.grpPreset = new System.Windows.Forms.GroupBox();
            this.grpDownload = new System.Windows.Forms.GroupBox();
            this.grpIcons = new System.Windows.Forms.GroupBox();
            this.grpNetwork = new System.Windows.Forms.GroupBox();
            this.grpProviders = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.numMaxIconSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).BeginInit();
            this.grpPreset.SuspendLayout();
            this.grpDownload.SuspendLayout();
            this.grpIcons.SuspendLayout();
            this.grpNetwork.SuspendLayout();
            this.grpProviders.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpPreset
            // 
            this.grpPreset.Text = "Fetch Preset";
            this.grpPreset.Location = new System.Drawing.Point(12, 12);
            this.grpPreset.Size = new System.Drawing.Size(596, 95);
            this.grpPreset.Controls.Add(this.lblFetchPreset);
            this.grpPreset.Controls.Add(this.cmbFetchPreset);
            this.grpPreset.Controls.Add(this.lblFetchPresetDescription);
            // 
            // lblFetchPreset
            // 
            this.lblFetchPreset.Text = "Mode:";
            this.lblFetchPreset.Location = new System.Drawing.Point(15, 28);
            this.lblFetchPreset.Size = new System.Drawing.Size(120, 20);
            this.lblFetchPreset.AutoSize = true;
            // 
            // cmbFetchPreset
            // 
            this.cmbFetchPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFetchPreset.Location = new System.Drawing.Point(145, 25);
            this.cmbFetchPreset.Size = new System.Drawing.Size(220, 23);
            this.cmbFetchPreset.SelectedIndexChanged += new System.EventHandler(this.cmbFetchPreset_SelectedIndexChanged);
            // 
            // lblFetchPresetDescription
            // 
            this.lblFetchPresetDescription.Location = new System.Drawing.Point(15, 55);
            this.lblFetchPresetDescription.Size = new System.Drawing.Size(560, 32);
            this.lblFetchPresetDescription.Text = "Preset description";
            // 
            //
            // grpDownload
            //
            this.grpDownload.Text = "Download Options";
            this.grpDownload.Location = new System.Drawing.Point(12, 115);
            this.grpDownload.Size = new System.Drawing.Size(596, 100);
            this.grpDownload.Controls.Add(this.chkPrefixUrls);
            this.grpDownload.Controls.Add(this.chkUseTitleField);
            this.grpDownload.Controls.Add(this.chkAutoSave);
            //
            // chkPrefixUrls
            //
            this.chkPrefixUrls.Text = "Automatically prefix URLs with https://";
            this.chkPrefixUrls.Location = new System.Drawing.Point(15, 25);
            this.chkPrefixUrls.Size = new System.Drawing.Size(400, 22);
            this.chkPrefixUrls.AutoSize = true;
            //
            // chkUseTitleField
            //
            this.chkUseTitleField.Text = "Use Title field if URL field is empty";
            this.chkUseTitleField.Location = new System.Drawing.Point(15, 50);
            this.chkUseTitleField.Size = new System.Drawing.Size(400, 22);
            this.chkUseTitleField.AutoSize = true;
            //
            // chkAutoSave
            //
            this.chkAutoSave.Text = "Automatically save database after downloading";
            this.chkAutoSave.Location = new System.Drawing.Point(15, 75);
            this.chkAutoSave.Size = new System.Drawing.Size(400, 22);
            this.chkAutoSave.AutoSize = true;
            //
            // grpIcons
            //
            this.grpIcons.Text = "Icon Options";
            this.grpIcons.Location = new System.Drawing.Point(12, 223);
            this.grpIcons.Size = new System.Drawing.Size(596, 118);
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
            this.chkSkipExistingIcons.Size = new System.Drawing.Size(400, 22);
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
            this.numMaxIconSize.Location = new System.Drawing.Point(180, 53);
            this.numMaxIconSize.Size = new System.Drawing.Size(70, 23);
            this.numMaxIconSize.Minimum = 16;
            this.numMaxIconSize.Maximum = 256;
            this.numMaxIconSize.Value = 128;
            this.numMaxIconSize.Increment = 16;
            //
            // lblIconSizeUnit
            //
            this.lblIconSizeUnit.Text = "px";
            this.lblIconSizeUnit.Location = new System.Drawing.Point(260, 55);
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
            this.txtIconPrefix.Location = new System.Drawing.Point(180, 88);
            this.txtIconPrefix.Size = new System.Drawing.Size(260, 23);
            //
            // grpNetwork
            //
            this.grpNetwork.Text = "Network and Fallback Options";
            this.grpNetwork.Location = new System.Drawing.Point(12, 349);
            this.grpNetwork.Size = new System.Drawing.Size(596, 135);
            this.grpNetwork.Controls.Add(this.chkAllowSelfSigned);
            this.grpNetwork.Controls.Add(this.lblTimeout);
            this.grpNetwork.Controls.Add(this.numTimeout);
            this.grpNetwork.Controls.Add(this.lblTimeoutUnit);
            this.grpNetwork.Controls.Add(this.chkUseThirdPartyFallbacks);
            this.grpNetwork.Controls.Add(this.chkAllowSyntheticFallbacks);
            //
            // chkAllowSelfSigned
            //
            this.chkAllowSelfSigned.Text = "Allow self-signed SSL certificates (weakens TLS validation globally)";
            this.chkAllowSelfSigned.Location = new System.Drawing.Point(15, 25);
            this.chkAllowSelfSigned.Size = new System.Drawing.Size(560, 22);
            this.chkAllowSelfSigned.AutoSize = false;
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
            this.numTimeout.Location = new System.Drawing.Point(180, 56);
            this.numTimeout.Size = new System.Drawing.Size(70, 23);
            this.numTimeout.Minimum = 5;
            this.numTimeout.Maximum = 60;
            this.numTimeout.Value = 15;
            //
            // lblTimeoutUnit
            //
            this.lblTimeoutUnit.Text = "seconds";
            this.lblTimeoutUnit.Location = new System.Drawing.Point(260, 58);
            this.lblTimeoutUnit.Size = new System.Drawing.Size(60, 20);
            this.lblTimeoutUnit.AutoSize = true;
            //
            // chkUseThirdPartyFallbacks
            //
            this.chkUseThirdPartyFallbacks.Text = "Use third-party favicon services";
            this.chkUseThirdPartyFallbacks.Location = new System.Drawing.Point(15, 88);
            this.chkUseThirdPartyFallbacks.Size = new System.Drawing.Size(400, 22);
            this.chkUseThirdPartyFallbacks.AutoSize = true;
            //
            // chkAllowSyntheticFallbacks
            //
            this.chkAllowSyntheticFallbacks.Text = "Allow synthetic fallback icons (last resort)";
            this.chkAllowSyntheticFallbacks.Location = new System.Drawing.Point(15, 113);
            this.chkAllowSyntheticFallbacks.Size = new System.Drawing.Size(400, 22);
            this.chkAllowSyntheticFallbacks.AutoSize = true;
            //
            // grpProviders
            //
            this.grpProviders.Text = "Provider Controls";
            this.grpProviders.Location = new System.Drawing.Point(12, 492);
            this.grpProviders.Size = new System.Drawing.Size(596, 328);
            this.grpProviders.Controls.Add(this.chkProviderDirectSite);
            this.grpProviders.Controls.Add(this.chkProviderTwentyIcons);
            this.grpProviders.Controls.Add(this.chkProviderDuckDuckGo);
            this.grpProviders.Controls.Add(this.chkProviderGoogle);
            this.grpProviders.Controls.Add(this.chkProviderYandex);
            this.grpProviders.Controls.Add(this.chkProviderFavicone);
            this.grpProviders.Controls.Add(this.chkProviderIconHorse);
            this.grpProviders.Controls.Add(this.lblProviderOrderHint);
            this.grpProviders.Controls.Add(this.lstProviderOrder);
            this.grpProviders.Controls.Add(this.btnProviderUp);
            this.grpProviders.Controls.Add(this.btnProviderDown);
            this.grpProviders.Controls.Add(this.btnProviderReset);
            //
            // chkProviderDirectSite
            //
            this.chkProviderDirectSite.Text = "Enable Direct Site";
            this.chkProviderDirectSite.Location = new System.Drawing.Point(15, 22);
            this.chkProviderDirectSite.Size = new System.Drawing.Size(180, 22);
            this.chkProviderDirectSite.AutoSize = true;
            //
            // chkProviderTwentyIcons
            //
            this.chkProviderTwentyIcons.Text = "Enable Twenty Icons";
            this.chkProviderTwentyIcons.Location = new System.Drawing.Point(290, 22);
            this.chkProviderTwentyIcons.Size = new System.Drawing.Size(180, 22);
            this.chkProviderTwentyIcons.AutoSize = true;
            //
            // chkProviderDuckDuckGo
            //
            this.chkProviderDuckDuckGo.Text = "Enable DuckDuckGo";
            this.chkProviderDuckDuckGo.Location = new System.Drawing.Point(15, 47);
            this.chkProviderDuckDuckGo.Size = new System.Drawing.Size(180, 22);
            this.chkProviderDuckDuckGo.AutoSize = true;
            //
            // chkProviderGoogle
            //
            this.chkProviderGoogle.Text = "Enable Google";
            this.chkProviderGoogle.Location = new System.Drawing.Point(290, 47);
            this.chkProviderGoogle.Size = new System.Drawing.Size(180, 22);
            this.chkProviderGoogle.AutoSize = true;
            //
            // chkProviderYandex
            //
            this.chkProviderYandex.Text = "Enable Yandex";
            this.chkProviderYandex.Location = new System.Drawing.Point(15, 72);
            this.chkProviderYandex.Size = new System.Drawing.Size(180, 22);
            this.chkProviderYandex.AutoSize = true;
            //
            // chkProviderFavicone
            //
            this.chkProviderFavicone.Text = "Enable Favicone";
            this.chkProviderFavicone.Location = new System.Drawing.Point(290, 72);
            this.chkProviderFavicone.Size = new System.Drawing.Size(180, 22);
            this.chkProviderFavicone.AutoSize = true;
            //
            // chkProviderIconHorse
            //
            this.chkProviderIconHorse.Text = "Enable Icon Horse";
            this.chkProviderIconHorse.Location = new System.Drawing.Point(15, 97);
            this.chkProviderIconHorse.Size = new System.Drawing.Size(180, 22);
            this.chkProviderIconHorse.AutoSize = true;
            //
            // lblProviderOrderHint
            //
            this.lblProviderOrderHint.Text = "Provider order: select one and move it up or down.";
            this.lblProviderOrderHint.Location = new System.Drawing.Point(15, 122);
            this.lblProviderOrderHint.Size = new System.Drawing.Size(540, 20);
            this.lblProviderOrderHint.AutoSize = true;
            //
            // lstProviderOrder
            //
            this.lstProviderOrder.Location = new System.Drawing.Point(15, 147);
            this.lstProviderOrder.Size = new System.Drawing.Size(400, 162);
            this.lstProviderOrder.IntegralHeight = false;
            this.lstProviderOrder.SelectedIndexChanged += new System.EventHandler(this.lstProviderOrder_SelectedIndexChanged);
            //
            // btnProviderUp
            //
            this.btnProviderUp.Text = "Move Up";
            this.btnProviderUp.Location = new System.Drawing.Point(430, 147);
            this.btnProviderUp.Size = new System.Drawing.Size(130, 28);
            this.btnProviderUp.Click += new System.EventHandler(this.btnProviderUp_Click);
            //
            // btnProviderDown
            //
            this.btnProviderDown.Text = "Move Down";
            this.btnProviderDown.Location = new System.Drawing.Point(430, 183);
            this.btnProviderDown.Size = new System.Drawing.Size(130, 28);
            this.btnProviderDown.Click += new System.EventHandler(this.btnProviderDown_Click);
            //
            // btnProviderReset
            //
            this.btnProviderReset.Text = "Reset";
            this.btnProviderReset.Location = new System.Drawing.Point(430, 219);
            this.btnProviderReset.Size = new System.Drawing.Size(130, 28);
            this.btnProviderReset.Click += new System.EventHandler(this.btnProviderReset_Click);
            //
            // btnOK
            //
            this.btnOK.Text = "OK";
            this.btnOK.Location = new System.Drawing.Point(452, 832);
            this.btnOK.Size = new System.Drawing.Size(75, 28);
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel
            //
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new System.Drawing.Point(533, 832);
            this.btnCancel.Size = new System.Drawing.Size(75, 28);
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // SettingsForm
            //
            this.Text = "KeeFetch Settings";
            this.ClientSize = new System.Drawing.Size(620, 872);
            this.MinimumSize = new System.Drawing.Size(636, 911);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.grpPreset);
            this.Controls.Add(this.grpDownload);
            this.Controls.Add(this.grpIcons);
            this.Controls.Add(this.grpNetwork);
            this.Controls.Add(this.grpProviders);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            ((System.ComponentModel.ISupportInitialize)(this.numMaxIconSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeout)).EndInit();
            this.grpPreset.ResumeLayout(false);
            this.grpPreset.PerformLayout();
            this.grpDownload.ResumeLayout(false);
            this.grpDownload.PerformLayout();
            this.grpIcons.ResumeLayout(false);
            this.grpIcons.PerformLayout();
            this.grpNetwork.ResumeLayout(false);
            this.grpNetwork.PerformLayout();
            this.grpProviders.ResumeLayout(false);
            this.grpProviders.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
