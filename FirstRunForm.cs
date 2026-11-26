using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using KeeFetch.FetchProfiles;
using KeePassLib.Utility;

namespace KeeFetch
{
    internal sealed partial class FirstRunForm : Form
    {
        internal const string PrivacyUrl = "https://tzii.github.io/KeeFetch/privacy.html";

        private const int DetailIndent = 20;
        private readonly List<RadioButton> profileButtons = new List<RadioButton>();
        private readonly List<Label> wrappedLabels = new List<Label>();

        public FirstRunForm(string initialProfileId)
            : this(initialProfileId, null)
        {
        }

        /// <param name="notice">Optional bold notice above the choices, e.g. why the dialog is shown again.</param>
        public FirstRunForm(string initialProfileId, string notice)
        {
            InitializeComponent();
            UiAssets.PrepareDialog(this);
            lnkPrivacy.Links.Add(0, lnkPrivacy.Text.Length, PrivacyUrl);
            lblPrivacyDisclosure.Text = BuildDisclosure();
            if (!string.IsNullOrWhiteSpace(notice))
            {
                lblNotice.Text = notice;
                lblNotice.Visible = true;
            }

            wrappedLabels.Add(lblIntro);
            wrappedLabels.Add(lblNotice);
            wrappedLabels.Add(lblPrivacyDisclosure);
            LoadProfiles(initialProfileId);
            Resize += delegate { UpdateWrapWidths(); };
            UpdateWrapWidths();
        }

        public string SelectedProfileId { get; private set; }
        public bool Confirmed { get; private set; }

        /// <summary>Visible profile ids in display order.</summary>
        internal IList<string> ProfileIds
        {
            get
            {
                List<string> ids = new List<string>();
                foreach (RadioButton button in profileButtons)
                    ids.Add((string)button.Tag);
                return ids;
            }
        }

        internal static string BuildDisclosure()
        {
            string services = ProfilePresentation.ThirdPartyNames(LegacyProfileMigration.DefaultProfileId);
            return "KeeFetch uses only each entry's web address. Usernames, passwords and notes are never sent. " +
                "Balanced may share an entry's domain with the third-party icon services " + services +
                " when the website itself has no usable icon. Privacy never uses third-party icon services, " +
                "although a website can still point to an icon stored on another server.";
        }

        private void LoadProfiles(string initialProfileId)
        {
            profileLayout.SuspendLayout();
            profileLayout.Controls.Clear();
            profileLayout.RowStyles.Clear();
            profileButtons.Clear();

            int tabIndex = 0;
            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles)
            {
                if (!profile.IsVisible)
                    continue;

                var button = new RadioButton();
                button.Name = "rdoProfile_" + profile.Id;
                button.AccessibleName = profile.DisplayName + " profile";
                button.AutoSize = true;
                button.Margin = new Padding(0, 4, 0, 0);
                button.TabIndex = tabIndex++;
                button.Tag = profile.Id;
                button.Text = ProfilePresentation.ChoiceName(profile);
                button.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
                button.CheckedChanged += profileButton_CheckedChanged;

                var detail = new Label();
                detail.Name = "lblProfile_" + profile.Id;
                detail.AutoSize = true;
                detail.Margin = new Padding(DetailIndent, 2, 0, 8);
                detail.Text = ProfilePresentation.Summary(profile.Id) + Environment.NewLine +
                    ProfilePresentation.ContactsLine(profile.Id);

                profileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                profileLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                profileLayout.Controls.Add(button, 0, profileLayout.RowStyles.Count - 2);
                profileLayout.Controls.Add(detail, 0, profileLayout.RowStyles.Count - 1);
                profileButtons.Add(button);
                wrappedLabels.Add(detail);
            }

            profileLayout.RowCount = profileLayout.RowStyles.Count;
            profileLayout.ResumeLayout(true);

            string target = string.IsNullOrWhiteSpace(initialProfileId)
                ? LegacyProfileMigration.DefaultProfileId
                : initialProfileId.Trim();
            if (!SelectProfile(target))
                SelectProfile(LegacyProfileMigration.DefaultProfileId);
            if (SelectedProfileId == null && profileButtons.Count > 0)
                profileButtons[0].Checked = true;

            UpdateSelectedProfile();
        }

        internal bool SelectProfile(string profileId)
        {
            foreach (RadioButton button in profileButtons)
            {
                if (string.Equals((string)button.Tag, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    button.Checked = true;
                    UpdateSelectedProfile();
                    return true;
                }
            }

            return false;
        }

        private void profileButton_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelectedProfile();
        }

        private void UpdateSelectedProfile()
        {
            SelectedProfileId = null;
            foreach (RadioButton button in profileButtons)
            {
                if (button.Checked)
                    SelectedProfileId = (string)button.Tag;
            }

            btnConfirm.Enabled = SelectedProfileId != null;
        }

        private void UpdateWrapWidths()
        {
            // The root layout fills the form; use the form's width because the
            // layout has not been sized yet while the constructor runs.
            int available = ClientSize.Width - rootLayout.Padding.Horizontal;
            if (available <= 0)
                return;

            // Each label's margin already includes its indent.
            foreach (Label label in wrappedLabels)
            {
                int width = Math.Max(120, available - label.Margin.Horizontal);
                label.MaximumSize = new System.Drawing.Size(width, 0);
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedProfileId))
                return;

            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void lnkPrivacy_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(PrivacyUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageService.ShowInfo(
                    "KeeFetch could not open the privacy page. Open this address manually:",
                    PrivacyUrl, ex.Message);
            }
        }
    }
}
