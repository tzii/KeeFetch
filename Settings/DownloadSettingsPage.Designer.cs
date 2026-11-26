using System.Drawing;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class DownloadSettingsPage
    {
        private TableLayoutPanel layout;
        private Label lblHeading;
        private CheckBox chkPrefixUrls;
        private CheckBox chkUseTitleField;
        private CheckBox chkSkipExistingIcons;
        private CheckBox chkAutoSave;
        private Label lblMaxIconSize;
        private NumericUpDown numMaxIconSize;
        private Label lblIconNamePrefix;
        private TextBox txtIconNamePrefix;
        private Label lblTitleFieldHint;
        private FlowLayoutPanel sizePanel;
        private Label lblPixels;

        private void InitializeComponent()
        {
            layout = new TableLayoutPanel();
            lblHeading = new Label();
            chkPrefixUrls = new CheckBox();
            chkUseTitleField = new CheckBox();
            chkSkipExistingIcons = new CheckBox();
            chkAutoSave = new CheckBox();
            lblMaxIconSize = new Label();
            numMaxIconSize = new NumericUpDown();
            lblIconNamePrefix = new Label();
            txtIconNamePrefix = new TextBox();
            lblTitleFieldHint = new Label();
            sizePanel = new FlowLayoutPanel();
            lblPixels = new Label();
            ((System.ComponentModel.ISupportInitialize)numMaxIconSize).BeginInit();
            SuspendLayout();

            layout.Name = "downloadLayout";
            layout.ColumnCount = 2;
            layout.RowCount = 9;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);

            lblHeading.Name = "lblDownloadHeading";
            lblHeading.Text = "Download behavior";
            lblHeading.AutoSize = true;
            lblHeading.Font = new Font(Font, FontStyle.Bold);
            lblHeading.Margin = new Padding(0, 0, 0, 12);
            layout.SetColumnSpan(lblHeading, 2);

            ConfigureCheckBox(chkPrefixUrls, "chkPrefixUrls", "&Add https:// to URLs that have no scheme", "Automatically prefix URLs", 0);
            ConfigureCheckBox(chkUseTitleField, "chkUseTitleField", "&Guess a web address from the entry title when the URL is empty", "Use title fallback", 1);
            ConfigureCheckBox(chkSkipExistingIcons, "chkSkipExistingIcons", "S&kip entries that already have a custom icon when downloading a group or the database", "Skip existing custom icons", 2);
            ConfigureCheckBox(chkAutoSave, "chkAutoSave", "Save the &database after icons are applied", "Save database after download", 3);

            lblTitleFieldHint.Name = "lblTitleFieldHint";
            lblTitleFieldHint.Text = "A guessed address such as mybank.com may be sent to the profile's icon services.";
            lblTitleFieldHint.AutoSize = true;
            lblTitleFieldHint.MaximumSize = new Size(600, 0);
            lblTitleFieldHint.ForeColor = SystemColors.GrayText;
            lblTitleFieldHint.Margin = new Padding(18, 0, 0, 6);
            layout.SetColumnSpan(lblTitleFieldHint, 2);

            lblMaxIconSize.Name = "lblMaxIconSize";
            lblMaxIconSize.Text = "Maximum icon si&ze:";
            lblMaxIconSize.AutoSize = true;
            lblMaxIconSize.Anchor = AnchorStyles.Left;
            lblMaxIconSize.Margin = new Padding(0, 10, 12, 4);

            numMaxIconSize.Name = "numMaxIconSize";
            numMaxIconSize.AccessibleName = "Maximum icon size in pixels";
            numMaxIconSize.Minimum = 16;
            numMaxIconSize.Maximum = 256;
            numMaxIconSize.Increment = 16;
            numMaxIconSize.Value = 128;
            numMaxIconSize.Width = 72;
            numMaxIconSize.TabIndex = 0;
            numMaxIconSize.Margin = new Padding(0, 0, 6, 0);

            lblPixels.Name = "lblPixels";
            lblPixels.Text = "px";
            lblPixels.AutoSize = true;
            lblPixels.Margin = new Padding(0, 3, 0, 0);

            sizePanel.Name = "sizePanel";
            sizePanel.AutoSize = true;
            sizePanel.FlowDirection = FlowDirection.LeftToRight;
            sizePanel.WrapContents = false;
            sizePanel.Anchor = AnchorStyles.Left;
            sizePanel.Margin = new Padding(0, 8, 0, 4);
            sizePanel.TabIndex = 4;
            sizePanel.Controls.Add(numMaxIconSize);
            sizePanel.Controls.Add(lblPixels);

            lblIconNamePrefix.Name = "lblIconNamePrefix";
            lblIconNamePrefix.Text = "Icon name &prefix:";
            lblIconNamePrefix.AutoSize = true;
            lblIconNamePrefix.Anchor = AnchorStyles.Left;
            lblIconNamePrefix.Margin = new Padding(0, 10, 12, 4);

            txtIconNamePrefix.Name = "txtIconNamePrefix";
            txtIconNamePrefix.AccessibleName = "Icon name prefix";
            txtIconNamePrefix.Anchor = AnchorStyles.Left;
            txtIconNamePrefix.Width = 160;
            txtIconNamePrefix.TabIndex = 5;
            txtIconNamePrefix.Margin = new Padding(0, 8, 0, 4);

            layout.Controls.Add(lblHeading, 0, 0);
            layout.Controls.Add(chkPrefixUrls, 0, 1);
            layout.Controls.Add(chkUseTitleField, 0, 2);
            layout.Controls.Add(lblTitleFieldHint, 0, 3);
            layout.Controls.Add(chkSkipExistingIcons, 0, 4);
            layout.Controls.Add(chkAutoSave, 0, 5);
            layout.Controls.Add(lblMaxIconSize, 0, 6);
            layout.Controls.Add(sizePanel, 1, 6);
            layout.Controls.Add(lblIconNamePrefix, 0, 7);
            layout.Controls.Add(txtIconNamePrefix, 1, 7);

            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Controls.Add(layout);
            Dock = DockStyle.Fill;
            Name = "DownloadSettingsPage";
            Size = new Size(680, 420);
            TabStop = false;
            ((System.ComponentModel.ISupportInitialize)numMaxIconSize).EndInit();
            ResumeLayout(false);
        }

        private void ConfigureCheckBox(CheckBox checkBox, string name, string text,
            string accessibleName, int tabIndex)
        {
            checkBox.Name = name;
            checkBox.Text = text;
            checkBox.AccessibleName = accessibleName;
            checkBox.AutoSize = true;
            checkBox.TabIndex = tabIndex;
            checkBox.Margin = new Padding(0, 4, 0, 6);
            layout.SetColumnSpan(checkBox, 2);
        }
    }
}
