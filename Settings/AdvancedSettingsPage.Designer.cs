using System.Drawing;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class AdvancedSettingsPage
    {
        private TableLayoutPanel layout;
        private Label lblHeading;
        private Label lblTimeout;
        private NumericUpDown numTimeout;
        private CheckBox chkAllowSelfSigned;
        private CheckBox chkAllowSyntheticFallbacks;
        private Label lblManagedHint;
        private Button btnResetAdvanced;

        private void InitializeComponent()
        {
            layout = new TableLayoutPanel();
            lblHeading = new Label();
            lblTimeout = new Label();
            numTimeout = new NumericUpDown();
            chkAllowSelfSigned = new CheckBox();
            chkAllowSyntheticFallbacks = new CheckBox();
            lblManagedHint = new Label();
            btnResetAdvanced = new Button();
            ((System.ComponentModel.ISupportInitialize)numTimeout).BeginInit();
            SuspendLayout();

            layout.Name = "advancedLayout";
            layout.ColumnCount = 2;
            layout.RowCount = 7;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);

            lblHeading.Name = "lblAdvancedHeading";
            lblHeading.Text = "Advanced behavior";
            lblHeading.AutoSize = true;
            lblHeading.Font = new Font(Font, FontStyle.Bold);
            lblHeading.Margin = new Padding(0, 0, 0, 12);
            layout.SetColumnSpan(lblHeading, 2);

            lblTimeout.Name = "lblTimeout";
            lblTimeout.Text = "Connection timeout (seconds):";
            lblTimeout.AutoSize = true;
            lblTimeout.Anchor = AnchorStyles.Left;
            lblTimeout.Margin = new Padding(0, 4, 12, 8);

            numTimeout.Name = "numTimeout";
            numTimeout.AccessibleName = "Connection timeout in seconds";
            numTimeout.Minimum = 5;
            numTimeout.Maximum = 60;
            numTimeout.Value = 15;
            numTimeout.Anchor = AnchorStyles.Left;
            numTimeout.TabIndex = 0;
            numTimeout.Margin = new Padding(0, 0, 0, 8);

            chkAllowSelfSigned.Name = "chkAllowSelfSigned";
            chkAllowSelfSigned.Text = "Allow self-signed TLS certificates (weakens validation globally)";
            chkAllowSelfSigned.AccessibleName = "Allow self-signed TLS certificates";
            chkAllowSelfSigned.AutoSize = true;
            chkAllowSelfSigned.TabIndex = 1;
            chkAllowSelfSigned.Margin = new Padding(0, 4, 0, 8);
            layout.SetColumnSpan(chkAllowSelfSigned, 2);

            chkAllowSyntheticFallbacks.Name = "chkAllowSyntheticFallbacks";
            chkAllowSyntheticFallbacks.Text = "Allow generated fallback icons when no real icon is found";
            chkAllowSyntheticFallbacks.AccessibleName = "Allow generated fallback icons";
            chkAllowSyntheticFallbacks.AutoSize = true;
            chkAllowSyntheticFallbacks.TabIndex = 2;
            chkAllowSyntheticFallbacks.Margin = new Padding(0, 4, 0, 8);
            layout.SetColumnSpan(chkAllowSyntheticFallbacks, 2);

            lblManagedHint.Name = "lblManagedHint";
            lblManagedHint.AutoSize = true;
            lblManagedHint.Margin = new Padding(0, 4, 0, 12);
            layout.SetColumnSpan(lblManagedHint, 2);

            btnResetAdvanced.Name = "btnResetAdvanced";
            btnResetAdvanced.Text = "Reset advanced defaults";
            btnResetAdvanced.AccessibleName = "Reset advanced settings to defaults";
            btnResetAdvanced.AutoSize = true;
            btnResetAdvanced.Anchor = AnchorStyles.Left;
            btnResetAdvanced.TabIndex = 3;
            btnResetAdvanced.UseVisualStyleBackColor = true;
            btnResetAdvanced.Click += btnResetAdvanced_Click;
            layout.SetColumnSpan(btnResetAdvanced, 2);

            layout.Controls.Add(lblHeading, 0, 0);
            layout.Controls.Add(lblTimeout, 0, 1);
            layout.Controls.Add(numTimeout, 1, 1);
            layout.Controls.Add(chkAllowSelfSigned, 0, 2);
            layout.Controls.Add(chkAllowSyntheticFallbacks, 0, 3);
            layout.Controls.Add(lblManagedHint, 0, 4);
            layout.Controls.Add(btnResetAdvanced, 0, 5);

            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Controls.Add(layout);
            Dock = DockStyle.Fill;
            Name = "AdvancedSettingsPage";
            Size = new Size(680, 420);
            TabStop = false;
            ((System.ComponentModel.ISupportInitialize)numTimeout).EndInit();
            ResumeLayout(false);
        }
    }
}
