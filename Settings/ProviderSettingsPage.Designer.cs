using System.Drawing;
using System.Windows.Forms;

namespace KeeFetch.Settings
{
    internal sealed partial class ProviderSettingsPage
    {
        private TableLayoutPanel layout;
        private Label lblHeading;
        private Label lblProviderHint;
        private CheckedListBox clbProviders;
        private FlowLayoutPanel buttonPanel;
        private Button btnProviderUp;
        private Button btnProviderDown;
        private Button btnProviderReset;

        private void InitializeComponent()
        {
            layout = new TableLayoutPanel();
            lblHeading = new Label();
            lblProviderHint = new Label();
            clbProviders = new CheckedListBox();
            buttonPanel = new FlowLayoutPanel();
            btnProviderUp = new Button();
            btnProviderDown = new Button();
            btnProviderReset = new Button();
            SuspendLayout();

            layout.Name = "providerLayout";
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);

            lblHeading.Name = "lblProviderHeading";
            lblHeading.Text = "Providers";
            lblHeading.AutoSize = true;
            lblHeading.Font = new Font(Font, FontStyle.Bold);
            lblHeading.Margin = new Padding(0, 0, 0, 8);

            lblProviderHint.Name = "lblProviderHint";
            lblProviderHint.AutoSize = true;
            lblProviderHint.MaximumSize = new Size(620, 0);
            lblProviderHint.Margin = new Padding(0, 0, 0, 10);

            clbProviders.Name = "clbProviders";
            clbProviders.AccessibleName = "Enabled providers in execution order";
            clbProviders.CheckOnClick = true;
            clbProviders.Dock = DockStyle.Fill;
            clbProviders.IntegralHeight = false;
            clbProviders.TabIndex = 0;
            clbProviders.SelectedIndexChanged += clbProviders_SelectedIndexChanged;

            buttonPanel.Name = "providerButtonPanel";
            buttonPanel.AutoSize = true;
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.LeftToRight;
            buttonPanel.WrapContents = false;
            buttonPanel.Margin = new Padding(0, 10, 0, 0);

            ConfigureButton(btnProviderUp, "btnProviderUp", "Move &up", "Move selected provider up", 0, btnProviderUp_Click);
            ConfigureButton(btnProviderDown, "btnProviderDown", "Move down", "Move selected provider down", 1, btnProviderDown_Click);
            ConfigureButton(btnProviderReset, "btnProviderReset", "Reset", "Reset provider order and enabled providers", 2, btnProviderReset_Click);

            buttonPanel.Controls.Add(btnProviderUp);
            buttonPanel.Controls.Add(btnProviderDown);
            buttonPanel.Controls.Add(btnProviderReset);
            layout.Controls.Add(lblHeading, 0, 0);
            layout.Controls.Add(lblProviderHint, 0, 1);
            layout.Controls.Add(clbProviders, 0, 2);
            layout.Controls.Add(buttonPanel, 0, 3);

            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Controls.Add(layout);
            Dock = DockStyle.Fill;
            Name = "ProviderSettingsPage";
            Size = new Size(680, 420);
            TabStop = false;
            ResumeLayout(false);
        }

        private static void ConfigureButton(Button button, string name, string text,
            string accessibleName, int tabIndex, System.EventHandler handler)
        {
            button.Name = name;
            button.Text = text;
            button.AccessibleName = accessibleName;
            button.AutoSize = true;
            button.TabIndex = tabIndex;
            button.UseVisualStyleBackColor = true;
            button.Click += handler;
        }
    }
}
