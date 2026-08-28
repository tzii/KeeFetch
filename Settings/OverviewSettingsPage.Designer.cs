using System.Drawing;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class OverviewSettingsPage
    {
        private TableLayoutPanel layout;
        private Label lblHeading;
        private Label lblProfile;
        private ComboBox cmbProfile;
        private Label lblRecommended;
        private Label lblProfileDescription;

        private void InitializeComponent()
        {
            layout = new TableLayoutPanel();
            lblHeading = new Label();
            lblProfile = new Label();
            cmbProfile = new ComboBox();
            lblRecommended = new Label();
            lblProfileDescription = new Label();
            SuspendLayout();

            layout.Name = "overviewLayout";
            layout.ColumnCount = 2;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);

            lblHeading.Name = "lblOverviewHeading";
            lblHeading.Text = "Choose how KeeFetch finds icons";
            lblHeading.AutoSize = true;
            lblHeading.Font = new Font(Font, FontStyle.Bold);
            lblHeading.Margin = new Padding(0, 0, 0, 12);
            layout.SetColumnSpan(lblHeading, 2);

            lblProfile.Name = "lblProfile";
            lblProfile.Text = "&Profile:";
            lblProfile.AutoSize = true;
            lblProfile.Anchor = AnchorStyles.Left;
            lblProfile.Margin = new Padding(0, 4, 12, 4);

            cmbProfile.Name = "cmbProfile";
            cmbProfile.AccessibleName = "Fetch profile";
            cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProfile.Dock = DockStyle.Fill;
            cmbProfile.TabIndex = 0;
            cmbProfile.SelectedIndexChanged += cmbProfile_SelectedIndexChanged;

            lblRecommended.Name = "lblRecommended";
            lblRecommended.Text = "Balanced is recommended for most vaults.";
            lblRecommended.AutoSize = true;
            lblRecommended.Margin = new Padding(0, 8, 0, 8);
            layout.SetColumnSpan(lblRecommended, 2);

            lblProfileDescription.Name = "lblProfileDescription";
            lblProfileDescription.AutoSize = true;
            lblProfileDescription.Dock = DockStyle.Top;
            lblProfileDescription.MaximumSize = new Size(620, 0);
            lblProfileDescription.Margin = new Padding(0, 4, 0, 0);
            layout.SetColumnSpan(lblProfileDescription, 2);

            layout.Controls.Add(lblHeading, 0, 0);
            layout.Controls.Add(lblProfile, 0, 1);
            layout.Controls.Add(cmbProfile, 1, 1);
            layout.Controls.Add(lblRecommended, 0, 2);
            layout.Controls.Add(lblProfileDescription, 0, 3);

            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Controls.Add(layout);
            Dock = DockStyle.Fill;
            Name = "OverviewSettingsPage";
            Size = new Size(680, 420);
            TabStop = false;
            ResumeLayout(false);
        }
    }
}
