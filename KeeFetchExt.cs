using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;

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
            string resourceName = "KeeFetch.Assets.Icons." + fileName;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (var image = Image.FromStream(stream))
                    return new Bitmap(image, 16, 16);
            }
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
                MessageBox.Show("No entries selected.", "KeeFetch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunDownloadAsync(entries);
        }

        private async void OnDownloadGroup(object sender, EventArgs e)
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

            await RunDownloadAsync(entries.ToArray());
        }

        private async void OnDownloadAllEntries(object sender, EventArgs e)
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

            await RunDownloadAsync(entries.ToArray());
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

        private Task RunDownloadAsync(PwEntry[] entries)
        {
            if (!EnsureFirstRunDisclosure())
                return Task.CompletedTask;

            if (System.Threading.Interlocked.CompareExchange(ref activeDownloadJob, 1, 0) != 0)
            {
                MessageBox.Show(
                    "A KeeFetch download job is already running.",
                    "KeeFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            try
            {
                var dialog = new FaviconDialog(host, Config, entries);
                var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                dialog.RunAsync().ContinueWith(t =>
                {
                    System.Threading.Interlocked.Exchange(ref activeDownloadJob, 0);

                    if (t.IsFaulted && t.Exception != null)
                    {
                        Exception ex = t.Exception.GetBaseException();
                        Logger.Error("RunDownloadAsync", ex);
                        MessageBox.Show("An error occurred during favicon download:\n" + ex.Message,
                            "KeeFetch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, scheduler);
            }
            catch (Exception ex)
            {
                System.Threading.Interlocked.Exchange(ref activeDownloadJob, 0);
                Logger.Error("RunDownloadAsync", ex);
                MessageBox.Show("An error occurred during favicon download:\n" + ex.Message,
                    "KeeFetch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return Task.CompletedTask;
        }

        private bool EnsureFirstRunDisclosure()
        {
            if (Config.HasSeenFirstRunDisclosure)
                return true;

            MessageBox.Show(
                "KeeFetch is availability-first by default.\n\n" +
                "To maximize success rates, domain names may be sent to third-party favicon services " +
                "(Twenty Icons, DuckDuckGo, Google, Yandex, Favicone, Icon Horse) when direct fetching " +
                "is insufficient.\n\n" +
                "You can disable third-party or synthetic fallbacks in KeeFetch Settings.",
                "KeeFetch disclosure",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Config.HasSeenFirstRunDisclosure = true;
            return true;
        }

        public override string UpdateUrl
        {
            get { return "https://raw.githubusercontent.com/tzii/KeeFetch/master/version.txt"; }
        }
    }
}
