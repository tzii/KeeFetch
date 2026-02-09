using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;

namespace KeeFetch
{
    public sealed class KeeFetchExt : Plugin
    {
        private IPluginHost host;
        internal static Configuration Config;

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
                tsmi.Click += OnDownloadSelectedEntries;

                tsmi.DropDownOpening += (s, e2) => { };
                return tsmi;
            }

            if (t == PluginMenuType.Group)
            {
                var tsmi = new ToolStripMenuItem("KeeFetch - Download Favicons");

                var itemRecursive = new ToolStripMenuItem("Download for this group (recursive)");
                itemRecursive.Click += OnDownloadGroup;

                tsmi.DropDownItems.Add(itemRecursive);
                return tsmi;
            }

            if (t == PluginMenuType.Main)
            {
                var tsmi = new ToolStripMenuItem("KeeFetch");

                var itemAllEntries = new ToolStripMenuItem("Download All Favicons");
                itemAllEntries.Click += OnDownloadAllEntries;

                var itemSettings = new ToolStripMenuItem("Settings...");
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

        private void OnDownloadSelectedEntries(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwEntry[] entries = host.MainWindow.GetSelectedEntries();
            if (entries == null || entries.Length == 0)
            {
                MessageBox.Show("No entries selected.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunDownload(entries);
        }

        private void OnDownloadGroup(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwGroup group = host.MainWindow.GetSelectedGroup();
            if (group == null)
            {
                MessageBox.Show("No group selected.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var entryList = group.GetEntries(true);

            if (entryList == null || entryList.UCount == 0)
            {
                MessageBox.Show("No entries in this group.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var entries = new List<PwEntry>(entryList);

            var result = MessageBox.Show(
                string.Format("Download favicons for {0} entries in '{1}' (including subgroups)?",
                    entries.Count, group.Name),
                "KeeFetch", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            RunDownload(entries.ToArray());
        }

        private void OnDownloadAllEntries(object sender, EventArgs e)
        {
            if (!EnsureDbOpen()) return;

            PwGroup root = host.Database.RootGroup;
            var entryList = root.GetEntries(true);
            var entries = new List<PwEntry>(entryList);

            if (entries.Count == 0)
            {
                MessageBox.Show("Database has no entries.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var recycleBin = host.Database.RecycleBinUuid;
            if (!recycleBin.Equals(PwUuid.Zero))
            {
                entries.RemoveAll(entry =>
                {
                    PwGroup parent = entry.ParentGroup;
                    while (parent != null)
                    {
                        if (parent.Uuid.Equals(recycleBin))
                            return true;
                        parent = parent.ParentGroup;
                    }
                    return false;
                });
            }

            var result = MessageBox.Show(
                string.Format("Download favicons for all {0} entries?", entries.Count),
                "KeeFetch", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            RunDownload(entries.ToArray());
        }

        private void OnOpenSettings(object sender, EventArgs e)
        {
            using (var form = new SettingsForm(Config))
            {
                form.ShowDialog(host.MainWindow);
            }
        }

        private bool EnsureDbOpen()
        {
            if (host.Database == null || !host.Database.IsOpen)
            {
                MessageBox.Show("Please open a database first.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void RunDownload(PwEntry[] entries)
        {
            var dialog = new FaviconDialog(host, Config, entries);
            dialog.Run();
        }

        public override string UpdateUrl
        {
            get { return null; }
        }
    }
}
