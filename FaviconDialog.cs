using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Interfaces;

namespace KeeFetch
{
    internal sealed class FaviconDialog
    {
        private readonly IPluginHost host;
        private readonly Configuration config;
        private readonly PwEntry[] entries;
        private IStatusLogger logger;

        private int totalCount;
        private int successCount;
        private int skippedCount;
        private int notFoundCount;
        private int errorCount;
        private int processedCount;
        private bool dbModified;
        private int cancelled; // 0 = false, 1 = true (use Interlocked for thread-safe access)
        private int workerDone; // 0 = false, 1 = true (use Interlocked for thread-safe access)
        private int disposed; // 0 = false, 1 = true (prevents ObjectDisposedException on doneEvent/semaphore)
        private readonly List<string> errorLog = new List<string>();
        private readonly object errorLogLock = new object();

        // Concurrency limit to avoid ThreadPool starvation and excessive network load
        private const int MaxConcurrency = 8;

        public FaviconDialog(IPluginHost host, Configuration config, PwEntry[] entries)
        {
            this.host = host;
            this.config = config;
            this.entries = entries;
        }

        public void Run()
        {
            if (entries == null || entries.Length == 0)
                return;

            totalCount = entries.Length;
            successCount = 0;
            skippedCount = 0;
            notFoundCount = 0;
            errorCount = 0;
            processedCount = 0;
            dbModified = false;
            cancelled = 0;
            workerDone = 0;
            disposed = 0;

            logger = KeePass.UI.StatusUtil.CreateStatusDialog(
                host.MainWindow, out var statusForm,
                "KeeFetch - Downloading Favicons",
                "Downloading favicons for " + totalCount + " entries...",
                true, true);

            logger.StartLogging("Downloading favicons...", true);
            logger.SetProgress(0);
            logger.SetText(string.Format(
                "Starting download for {0} entries...", totalCount),
                LogStatusType.Info);

            // Run the work on a background thread, pump messages on the UI thread
            var workerThread = new Thread(WorkerThreadProc);
            workerThread.IsBackground = true;
            workerThread.Name = "KeeFetch-Worker";
            workerThread.Start();

            // Pump UI messages until the worker finishes
            while (Interlocked.CompareExchange(ref workerDone, 0, 0) == 0)
            {
                Application.DoEvents();
                Thread.Sleep(30);

                // Check for user cancellation via the status dialog
                if (Interlocked.CompareExchange(ref cancelled, 0, 0) == 0 && !logger.ContinueWork())
                {
                    Interlocked.Exchange(ref cancelled, 1);
                }
            }

            logger.EndLogging();
            ShowCompletionMessage();
        }

        private void WorkerThreadProc()
        {
            try
            {
                DoWork();
            }
            finally
            {
                Interlocked.Exchange(ref workerDone, 1);
            }
        }

        private bool IsCancelled()
        {
            return Interlocked.CompareExchange(ref cancelled, 0, 0) == 1;
        }

        private bool IsDisposed()
        {
            return Interlocked.CompareExchange(ref disposed, 0, 0) == 1;
        }

        private void DoWork()
        {
            FaviconDownloader.SetupTls();

            RemoteCertificateValidationCallback originalCallback =
                ServicePointManager.ServerCertificateValidationCallback;

            if (config.AllowSelfSignedCerts)
                FaviconDownloader.SetupSelfSignedCerts(true, originalCallback);

            try
            {
                IWebProxy proxy = Util.GetKeePassProxy();
                PwDatabase db = host.Database;

                int timeoutMs = config.Timeout * 1000;
                // Safety timeout: generous but not infinite
                int maxTotalWaitMs = Math.Max(timeoutMs * 5, entries.Length * 500);

                // Use a semaphore to limit concurrent downloads
                using (var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency))
                using (var doneEvent = new CountdownEvent(entries.Length))
                {
                    for (int idx = 0; idx < entries.Length; idx++)
                    {
                        if (IsCancelled())
                        {
                            // Signal remaining entries as done immediately
                            int unsignaled = entries.Length - idx;
                            for (int j = 0; j < unsignaled; j++)
                            {
                                Interlocked.Increment(ref processedCount);
                                Interlocked.Increment(ref skippedCount);
                                doneEvent.Signal();
                            }
                            break;
                        }

                        PwEntry entry = entries[idx];

                        // Wait for a semaphore slot (with timeout to check cancellation)
                        while (!semaphore.Wait(500))
                        {
                            if (IsCancelled())
                                break;
                        }

                        if (IsCancelled())
                        {
                            // Signal remaining entries (including current one)
                            int unsignaled = entries.Length - idx;
                            for (int j = 0; j < unsignaled; j++)
                            {
                                Interlocked.Increment(ref processedCount);
                                Interlocked.Increment(ref skippedCount);
                                doneEvent.Signal();
                            }
                            break;
                        }

                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                if (IsCancelled())
                                {
                                    Interlocked.Increment(ref skippedCount);
                                    return;
                                }

                                ProcessEntry(entry, db, proxy);
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref errorCount);
                                string title = "?";
                                string url = "?";
                                try { title = entry.Strings.ReadSafe(PwDefs.TitleField); } catch { }
                                try { url = entry.Strings.ReadSafe(PwDefs.UrlField); } catch { }
                                lock (errorLogLock)
                                {
                                    errorLog.Add(string.Format("[{0}] {1}: {2}", title, url, ex.ToString()));
                                }
                            }
                            finally
                            {
                                // Check if disposed before accessing synchronization primitives
                                if (!IsDisposed())
                                {
                                    try { semaphore.Release(); } catch (ObjectDisposedException) { }
                                }

                                int currentProcessed = Interlocked.Increment(ref processedCount);
                                int pct = (int)(currentProcessed * 100.0 / totalCount);
                                uint progressValue = (uint)Math.Min(Math.Max(pct, 0), 100);

                                int currentSuccess = Interlocked.CompareExchange(ref successCount, 0, 0);
                                int currentSkipped = Interlocked.CompareExchange(ref skippedCount, 0, 0);
                                int currentNotFound = Interlocked.CompareExchange(ref notFoundCount, 0, 0);
                                int currentErrors = Interlocked.CompareExchange(ref errorCount, 0, 0);

                                InvokeOnUI(() =>
                                {
                                    logger.SetProgress(progressValue);
                                    logger.SetText(string.Format(
                                        "Processed {0}/{1} ({2}%) — OK: {3}, Skipped: {4}, Not found: {5}, Errors: {6}",
                                        currentProcessed, totalCount, pct,
                                        currentSuccess, currentSkipped, currentNotFound, currentErrors),
                                        LogStatusType.Info);
                                });

                                // Check if disposed before signaling
                                if (!IsDisposed())
                                {
                                    try { doneEvent.Signal(); } catch (ObjectDisposedException) { }
                                }
                            }
                        });
                    }

                    // Wait with periodic timeout checks instead of blocking forever
                    int totalWaitedMs = 0;
                    int waitIntervalMs = 1000;

                    while (!doneEvent.Wait(waitIntervalMs))
                    {
                        totalWaitedMs += waitIntervalMs;

                        if (!logger.ContinueWork())
                        {
                            Interlocked.Exchange(ref cancelled, 1);
                        }

                        // Update UI so user sees the dialog is alive
                        int cp = Interlocked.CompareExchange(ref processedCount, 0, 0);
                        int cs = Interlocked.CompareExchange(ref successCount, 0, 0);
                        int csk = Interlocked.CompareExchange(ref skippedCount, 0, 0);
                        int cnf = Interlocked.CompareExchange(ref notFoundCount, 0, 0);
                        int ce = Interlocked.CompareExchange(ref errorCount, 0, 0);
                        InvokeOnUI(() =>
                        {
                            logger.SetText(string.Format(
                                "Waiting... Processed {0}/{1} — OK: {2}, Skipped: {3}, Not found: {4}, Errors: {5}",
                                cp, totalCount, cs, csk, cnf, ce),
                                LogStatusType.Info);
                        });

                        if (totalWaitedMs >= maxTotalWaitMs)
                        {
                            lock (errorLogLock)
                            {
                                errorLog.Add(string.Format(
                                    "Operation timed out after {0}ms. {1}/{2} entries were processed.",
                                    totalWaitedMs, processedCount, totalCount));
                            }
                            break;
                        }
                    }

                    // Mark as disposed before exiting using block to prevent ObjectDisposedException
                    Interlocked.Exchange(ref disposed, 1);
                }
            }
            finally
            {
                if (config.AllowSelfSignedCerts)
                    FaviconDownloader.SetupSelfSignedCerts(false, originalCallback);
            }
        }

        private void ProcessEntry(PwEntry entry, PwDatabase db, IWebProxy proxy)
        {
            if (config.SkipExistingIcons && !entry.CustomIconUuid.Equals(PwUuid.Zero))
            {
                Interlocked.Increment(ref skippedCount);
                return;
            }

            string url = Util.ResolveEntryUrl(entry, db);

            if (string.IsNullOrWhiteSpace(url) && config.UseTitleField)
            {
                string title = entry.Strings.ReadSafe(PwDefs.TitleField);
                url = Util.GuessDomainFromTitle(title);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                Interlocked.Increment(ref skippedCount);
                return;
            }

            if (config.PrefixUrls &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !AndroidAppMapper.IsAndroidUrl(url))
            {
                url = "https://" + url;
            }

            var downloader = new FaviconDownloader(config, proxy);
            FaviconResult result = downloader.Download(url);

            if (result.Status != FaviconStatus.Success || result.IconData == null)
            {
                Interlocked.Increment(ref notFoundCount);
                return;
            }

            byte[] iconHash = Util.HashData(result.IconData);
            byte[] iconData = result.IconData;
            string iconHost = result.Host;

            InvokeOnUI(() =>
            {
                lock (db)
                {
                    PwUuid iconUuid = new PwUuid(iconHash);

                    bool iconExists = db.CustomIcons.Any(ci => ci.Uuid.Equals(iconUuid));
                    if (!iconExists)
                    {
                        PwCustomIcon newIcon = new PwCustomIcon(iconUuid, iconData);

                        string iconName = config.IconNamePrefix;
                        if (!string.IsNullOrEmpty(iconName) && !string.IsNullOrEmpty(iconHost))
                            iconName += iconHost;
                        else if (!string.IsNullOrEmpty(iconHost))
                            iconName = iconHost;

                        if (!string.IsNullOrEmpty(iconName))
                        {
                            try
                            {
                                var nameProperty = newIcon.GetType().GetProperty("Name");
                                if (nameProperty != null)
                                    nameProperty.SetValue(newIcon, iconName);
                            }
                            catch { }
                        }

                        db.CustomIcons.Add(newIcon);
                    }

                    if (!entry.CustomIconUuid.Equals(iconUuid))
                    {
                        entry.CustomIconUuid = iconUuid;
                        entry.Touch(true, false);
                        dbModified = true;
                    }
                }
            });

            Interlocked.Increment(ref successCount);
        }

        private void InvokeOnUI(Action action)
        {
            try
            {
                var mainForm = host.MainWindow;
                if (mainForm != null && mainForm.InvokeRequired)
                    mainForm.BeginInvoke(action);
                else
                    action();
            }
            catch { }
        }

        private void ShowCompletionMessage()
        {
            if (dbModified)
            {
                try
                {
                    host.Database.UINeedsIconUpdate = true;
                    host.MainWindow.UpdateUI(false, null, false, null, true, null, true);

                    if (config.AutoSave && host.Database.IOConnectionInfo != null)
                    {
                        host.MainWindow.SaveDatabase(host.Database, null);
                    }
                }
                catch { }
            }

            string message = string.Format(
                "KeeFetch completed.\n\n" +
                "Total entries: {0}\n" +
                "Icons downloaded: {1}\n" +
                "Skipped: {2}\n" +
                "Not found: {3}\n" +
                "Errors: {4}",
                totalCount, successCount, skippedCount, notFoundCount, errorCount);

            if (IsCancelled())
                message += "\n\nDownload was cancelled by user.";

            if (errorLog.Count > 0)
            {
                try
                {
                    string logPath = Path.Combine(
                        Path.GetDirectoryName(host.Database.IOConnectionInfo.Path),
                        "KeeFetch_errors.log");
                    File.WriteAllText(logPath, string.Join(Environment.NewLine + Environment.NewLine, errorLog));
                    message += "\n\nError log saved to:\n" + logPath;
                }
                catch { }
            }

            MessageBox.Show(message, "KeeFetch", MessageBoxButtons.OK,
                IsCancelled() ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
