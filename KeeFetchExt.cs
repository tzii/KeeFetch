using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.FetchProfiles;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Utility;

namespace KeeFetch
{
    public sealed class KeeFetchExt : Plugin
    {
        private IPluginHost host;
        internal static Configuration Config;
        private static Image menuIcon;
        private static Image downloadIcon;
        private static Image settingsIcon;
        private static int activeDownloadJob;

        private static Image LoadEmbeddedIcon(string fileName)
        {
            return UiAssets.LoadImage(fileName, 16);
        }

        /// <summary>Icon shown for KeeFetch in KeePass's Tools > Plugins list.</summary>
        public override Image SmallIcon
        {
            get { return GetMenuIcon(); }
        }

        private static Image GetMenuIcon()
        {
            if (menuIcon != null)
                return menuIcon;

            menuIcon = LoadEmbeddedIcon("keefetch-app.png");
            if (menuIcon != null)
                return menuIcon;

            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var pen = new Pen(Color.FromArgb(90, 90, 90), 1.3f))
                {
                    g.DrawEllipse(pen, 1, 1, 12, 12);
                    g.DrawArc(pen, 3, 1, 6, 12, -90, 180);
                    g.DrawArc(pen, 5, 1, 6, 12, 90, 180);
                    g.DrawLine(pen, 1, 7, 13, 7);
                }

                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                {
                    g.FillRectangle(brush, 10, 7, 3, 4);
                    g.FillPolygon(brush, new[]
                    {
                        new Point(8, 11),
                        new Point(15, 11),
                        new Point(11, 15)
                    });
                }
            }

            menuIcon = bmp;
            return menuIcon;
        }

        private static Image GetDownloadIcon()
        {
            if (downloadIcon != null)
                return downloadIcon;

            downloadIcon = LoadEmbeddedIcon("keefetch-download.png");
            if (downloadIcon != null)
                return downloadIcon;

            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                {
                    g.FillPolygon(brush, new[]
                    {
                        new Point(8, 2),
                        new Point(13, 7),
                        new Point(10, 7),
                        new Point(10, 12),
                        new Point(6, 12),
                        new Point(6, 7),
                        new Point(3, 7)
                    });
                }

                using (var pen = new Pen(Color.FromArgb(90, 90, 90), 1.2f))
                    g.DrawLine(pen, 3, 14, 13, 14);
            }

            downloadIcon = bmp;
            return downloadIcon;
        }

        private static Image GetSettingsIcon()
        {
            if (settingsIcon != null)
                return settingsIcon;

            settingsIcon = LoadEmbeddedIcon("keefetch-settings.png");
            if (settingsIcon != null)
                return settingsIcon;

            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var pen = new Pen(Color.FromArgb(90, 90, 90), 1.3f))
                {
                    g.DrawEllipse(pen, 4, 4, 8, 8);
                    g.DrawLine(pen, 8, 1, 8, 4);
                    g.DrawLine(pen, 8, 12, 8, 15);
                    g.DrawLine(pen, 1, 8, 4, 8);
                    g.DrawLine(pen, 12, 8, 15, 8);
                    g.DrawLine(pen, 3, 3, 5, 5);
                    g.DrawLine(pen, 11, 11, 13, 13);
                    g.DrawLine(pen, 13, 3, 11, 5);
                    g.DrawLine(pen, 5, 11, 3, 13);
                }

                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                    g.FillEllipse(brush, 6, 6, 4, 4);
            }

            settingsIcon = bmp;
            return settingsIcon;
        }

        public override bool Initialize(IPluginHost pluginHost)
        {
            if (pluginHost == null) return false;
            host = pluginHost;
            Config = new Configuration(host.CustomConfig);
            return true;
        }

        public override void Terminate()
        {
        }

        public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
        {
            if (t == PluginMenuType.Entry)
            {
                var tsmi = new ToolStripMenuItem("KeeFetch - Download Favicons");
                tsmi.Image = GetMenuIcon();
                tsmi.Click += OnDownloadSelectedEntries;
                return tsmi;
            }

            if (t == PluginMenuType.Group)
            {
                var tsmi = new ToolStripMenuItem("KeeFetch - Download Favicons");
                tsmi.Image = GetMenuIcon();
                tsmi.Click += OnDownloadGroup;
                return tsmi;
            }

            if (t == PluginMenuType.Main)
            {
                var tsmi = new ToolStripMenuItem("KeeFetch");
                tsmi.Image = GetMenuIcon();

                var itemAllEntries = new ToolStripMenuItem("Download All Favicons");
                itemAllEntries.Image = GetDownloadIcon();
                itemAllEntries.Click += OnDownloadAllEntries;

                var itemSettings = new ToolStripMenuItem("Settings...");
                itemSettings.Image = GetSettingsIcon();
                itemSettings.Click += OnOpenSettings;

                var sep = new ToolStripSeparator();

                tsmi.DropDownItems.Add(itemAllEntries);
                tsmi.DropDownItems.Add(sep);
                tsmi.DropDownItems.Add(itemSettings);

                tsmi.DropDownOpening += (s, e2) =>
                {
                    bool dbOpen = host.Database != null && host.Database.IsOpen;
                    itemAllEntries.Enabled = dbOpen;
                };

                return tsmi;
            }

            return null;
        }

        private async void OnDownloadSelectedEntries(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwEntry[] entries = host.MainWindow.GetSelectedEntries();
            if (entries == null || entries.Length == 0)
            {
                MessageService.ShowInfo("Select one or more entries first.");
                return;
            }

            // Entries the user picked are always fetched, even when they
            // already have a custom icon: replacing it is the point.
            await RunDownloadAsync(entries, false);
        }

        private async void OnDownloadGroup(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwGroup group = host.MainWindow.GetSelectedGroup();
            if (group == null)
            {
                MessageService.ShowInfo("Select a group first.");
                return;
            }

            PwEntry[] entries = CollectEntries(host.Database, group);
            if (entries.Length == 0)
            {
                MessageService.ShowInfo(string.Format(
                    "The group '{0}' has no entries outside the Recycle Bin.", group.Name));
                return;
            }

            // The profile is chosen before the confirmation that describes it.
            if (!EnsureFirstRunDisclosure()) return;

            bool skipExisting = Config.SkipExistingIcons;
            string scope = string.Format("in '{0}' and its subgroups", group.Name);
            if (!ConfirmBulkRun(entries, skipExisting, scope)) return;

            await RunDownloadAsync(entries, skipExisting);
        }

        private async void OnDownloadAllEntries(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwEntry[] entries = CollectEntries(host.Database, host.Database.RootGroup);
            if (entries.Length == 0)
            {
                MessageService.ShowInfo("The database has no entries outside the Recycle Bin.");
                return;
            }

            if (!EnsureFirstRunDisclosure()) return;

            bool skipExisting = Config.SkipExistingIcons;
            if (!ConfirmBulkRun(entries, skipExisting, "in this database")) return;

            await RunDownloadAsync(entries, skipExisting);
        }

        /// <summary>
        /// Entries in a group and its subgroups, leaving out the Recycle Bin
        /// unless the chosen group is the Recycle Bin or inside it.
        /// </summary>
        internal static PwEntry[] CollectEntries(PwDatabase db, PwGroup group)
        {
            var entries = new List<PwEntry>();
            if (group == null)
                return entries.ToArray();

            PwUuid recycleBin = db != null ? db.RecycleBinUuid : PwUuid.Zero;
            bool includeRecycleBin = IsInGroup(group, recycleBin);
            foreach (PwEntry entry in group.GetEntries(true))
            {
                if (includeRecycleBin || !IsInGroup(entry.ParentGroup, recycleBin))
                    entries.Add(entry);
            }

            return entries.ToArray();
        }

        private static bool IsInGroup(PwGroup group, PwUuid ancestorUuid)
        {
            if (ancestorUuid == null || ancestorUuid.Equals(PwUuid.Zero))
                return false;

            for (PwGroup current = group; current != null; current = current.ParentGroup)
            {
                if (current.Uuid.Equals(ancestorUuid))
                    return true;
            }

            return false;
        }

        private static bool ConfirmBulkRun(PwEntry[] entries, bool skipExisting, string scope)
        {
            string question;
            if (!TryBuildBulkConfirmation(Config, entries, skipExisting, scope, out question))
            {
                MessageService.ShowInfo(question);
                return false;
            }

            return MessageService.AskYesNo(question, "KeeFetch", true, MessageBoxIcon.Question);
        }

        /// <summary>
        /// Builds the confirmation for a group or database run: how many
        /// entries are fetched, what the profile contacts, and what happens to
        /// existing custom icons. Returns false, with an explanation instead,
        /// when nothing would be fetched.
        /// </summary>
        internal static bool TryBuildBulkConfirmation(Configuration config, PwEntry[] entries,
            bool skipExisting, string scope, out string text)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            int total = entries == null ? 0 : entries.Length;
            int withCustomIcon = 0;
            if (entries != null)
            {
                foreach (PwEntry entry in entries)
                {
                    if (entry != null && !entry.CustomIconUuid.Equals(PwUuid.Zero))
                        withCustomIcon++;
                }
            }

            int toFetch = skipExisting ? total - withCustomIcon : total;
            if (toFetch <= 0)
            {
                text = string.Format(
                    "All {0} entries {1} already have a custom icon, so there is nothing to download." +
                    Environment.NewLine + Environment.NewLine +
                    "To replace existing icons, select the entries and choose KeeFetch - Download Favicons, " +
                    "or turn off skipping in Tools > KeeFetch > Settings > Downloads.",
                    total, scope);
                return false;
            }

            var lines = new List<string>();
            lines.Add(toFetch == total
                ? string.Format("Download favicons for {0} {1} {2}?", total, Plural(total, "entry", "entries"), scope)
                : string.Format("Download favicons for {0} of the {1} entries {2}?", toFetch, total, scope));
            lines.Add(string.Empty);
            lines.Add("Profile: " + ProfilePresentation.DisplayName(config.FetchProfileId));
            lines.Add(DescribeContacts(config));
            if (withCustomIcon > 0)
            {
                lines.Add(skipExisting
                    ? string.Format("{0} {1} that already {2} a custom icon {3} skipped.",
                        withCustomIcon, Plural(withCustomIcon, "entry", "entries"),
                        Plural(withCustomIcon, "has", "have"), Plural(withCustomIcon, "is", "are"))
                    : string.Format("{0} {1} already {2} a custom icon; KeeFetch replaces one only when it finds a new icon.",
                        withCustomIcon, Plural(withCustomIcon, "entry", "entries"),
                        Plural(withCustomIcon, "has", "have")));
            }
            if (config.AutoSave)
                lines.Add("The database is saved after the icons are applied.");

            text = string.Join(Environment.NewLine, lines.ToArray());
            return true;
        }

        private static string DescribeContacts(Configuration config)
        {
            try
            {
                FetchExecutionPolicy policy = FetchExecutionPolicy.Resolve(config);
                return ProfilePresentation.ContactsLine(policy.ProviderIds, policy.AllowAndroidStoreLookup);
            }
            catch (Exception ex)
            {
                // An invalid configuration is reported when the run starts.
                Logger.Debug("DescribeContacts", ex);
                return ProfilePresentation.ContactsLine(config.FetchProfileId);
            }
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count == 1 ? singular : plural;
        }

        private void OnOpenSettings(object sender, EventArgs e)
        {
            using (var form = new SettingsForm(Config, PersistConfiguration))
            {
                form.ShowDialog(host.MainWindow);
            }
        }

        private bool EnsureDbOpen()
        {
            if (host.Database == null || !host.Database.IsOpen)
            {
                MessageService.ShowWarning("Open a database first.");
                return false;
            }
            return true;
        }

        private async Task RunDownloadAsync(PwEntry[] entries, bool skipExistingCustomIcons)
        {
            if (!EnsureFirstRunDisclosure())
                return;

            if (System.Threading.Interlocked.CompareExchange(ref activeDownloadJob, 1, 0) != 0)
            {
                MessageService.ShowInfo("A KeeFetch download is already running.");
                return;
            }

            try
            {
                FaviconDialog dialog;
                if (!TryCreateFaviconDialog(entries, skipExistingCustomIcons, out dialog))
                    return;

                BatchRunResult result = await dialog.RunAsync();
                CompletionAction action = ShowCompletionResult(result, true);
                if (action == CompletionAction.RetryEligible && !result.WasCancelled &&
                    result.RetryEntries.Count > 0)
                {
                    FaviconDialog retryDialog;
                    if (!TryCreateFaviconDialog(result.RetryEntries.ToArray(), false, out retryDialog))
                        return;

                    // The retry belongs to the same batch, so undoing it
                    // reverts the first pass as well.
                    BatchRunResult retryResult = await retryDialog.RunAsync();
                    if (ShowCompletionResult(retryResult, false) == CompletionAction.Undo)
                        UndoRun(retryResult, result);
                }
                else if (action == CompletionAction.Undo)
                {
                    UndoRun(result);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RunDownloadAsync", ex);
                MessageService.ShowWarning("An error occurred during the favicon download:", ex.Message);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref activeDownloadJob, 0);
            }
        }

        private bool TryCreateFaviconDialog(PwEntry[] entries, bool skipExistingCustomIcons,
            out FaviconDialog dialog)
        {
            dialog = null;
            try
            {
                dialog = new FaviconDialog(host, Config, entries, skipExistingCustomIcons);
                return true;
            }
            catch (ArgumentException ex)
            {
                // Policy resolution is fail-closed: surface invalid provider
                // configuration visibly instead of a generic plugin crash.
                Logger.Error("RunDownloadAsync", ex);
                MessageService.ShowWarning("The current KeeFetch provider configuration is invalid:", ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error("RunDownloadAsync", ex);
                MessageService.ShowWarning("The current KeeFetch fetch profile is invalid:", ex.Message);
                return false;
            }
        }

        private CompletionAction ShowCompletionResult(BatchRunResult result, bool retryAllowed)
        {
            // A single entry that simply got its icon needs no report.
            if (IsCleanSingleUpdate(result))
            {
                SetStatus(string.Format("KeeFetch updated the icon of '{0}'.", result.Outcomes[0].Title));
                return CompletionAction.Close;
            }

            using (var form = new CompletionForm(result, retryAllowed))
            {
                form.ShowDialog(host.MainWindow);
                return form.SelectedAction;
            }
        }

        internal static bool IsCleanSingleUpdate(BatchRunResult result)
        {
            return result != null && !result.WasCancelled && result.TotalCount == 1 &&
                result.UpdatedCount == 1;
        }

        private void UndoRun(params BatchRunResult[] results)
        {
            int restored = 0;
            var touched = new List<PwDatabase>();
            foreach (BatchRunResult result in results)
            {
                PwDatabase db = result.Changes.Database;
                if (db == null)
                    continue;
                if (!db.IsOpen)
                {
                    MessageService.ShowWarning("The database is no longer open, so the run cannot be undone.");
                    return;
                }

                restored += result.Changes.Undo(db);
                if (!touched.Contains(db))
                    touched.Add(db);
            }

            foreach (PwDatabase db in touched)
                RefreshAfterUndo(db);

            SetStatus(string.Format("KeeFetch restored the previous icon of {0} {1}.",
                restored, Plural(restored, "entry", "entries")));
        }

        private void RefreshAfterUndo(PwDatabase db)
        {
            try
            {
                db.UINeedsIconUpdate = true;
                db.Modified = true;
                host.MainWindow.UpdateUI(false, null, false, null, true, null, true);
                if (Config.AutoSave && db.IOConnectionInfo != null)
                    host.MainWindow.SaveDatabase(db, null);
            }
            catch (Exception ex)
            {
                Logger.Error("UndoRun", ex);
            }
        }

        private void SetStatus(string text)
        {
            try
            {
                host.MainWindow.SetStatusEx(text);
            }
            catch (Exception ex)
            {
                Logger.Debug("SetStatus", ex);
            }
        }

        private bool EnsureFirstRunDisclosure()
        {
            // Reading the profile first applies any pending migration (a
            // retired v1.3 profile clears the disclosure flag) before the
            // flag is checked.
            string currentProfileId = Config.FetchProfileId;
            if (Config.HasSeenFirstRunDisclosure)
                return true;

            string notice = null;
            if (!string.IsNullOrEmpty(Config.RetiredProfileName))
            {
                notice = string.Format(
                    "KeeFetch 1.4 no longer offers the {0} profile. Choose a profile to continue.",
                    Config.RetiredProfileName);
            }

            using (var form = new FirstRunForm(currentProfileId, notice))
            {
                if (form.ShowDialog(host.MainWindow) != DialogResult.OK)
                    return false;

                return ApplyFirstRunChoice(Config, form, PersistConfiguration);
            }
        }

        internal static bool ApplyFirstRunChoice(Configuration config, FirstRunForm form, Action persist)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            if (form == null)
                throw new ArgumentNullException("form");
            if (!form.Confirmed)
                return false;

            FetchProfileDefinition profile;
            try
            {
                profile = FetchProfileCatalog.GetRequiredProfile(form.SelectedProfileId);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (!profile.IsVisible)
                return false;

            config.FetchProfileId = profile.Id;
            config.HasSeenFirstRunDisclosure = true;
            config.ClearRetiredProfileNotice();
            if (persist != null)
                persist();
            return true;
        }

        // KeePass flushes KeePass.config.xml itself only when its own state
        // changes, so plugin CustomConfig writes must be saved explicitly or
        // they are lost when the process exits.
        private void PersistConfiguration()
        {
            try
            {
                KeePass.App.Configuration.AppConfigSerializer.Save(KeePass.Program.Config);
            }
            catch (Exception ex)
            {
                Logger.Error("PersistConfiguration", ex);
            }
        }

        public override string UpdateUrl
        {
            get { return "https://raw.githubusercontent.com/tzii/KeeFetch/master/version.txt"; }
        }
    }
}
