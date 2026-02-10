using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Interfaces;

namespace KeeFetch
{
    /// <summary>
    /// Manages concurrent favicon downloads with progress reporting and cancellation support.
    /// </summary>
    internal sealed class FaviconDialog
    {
        private readonly IPluginHost host;
        private readonly Configuration config;
        private readonly PwEntry[] entries;
        private IStatusLogger logger;
        private CancellationTokenSource cts;

        private int totalCount;
        private int successCount;
        private int skippedCount;
        private int notFoundCount;
        private int errorCount;
        private int processedCount;
        private bool dbModified;
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

        /// <summary>
        /// Runs the download dialog asynchronously. Must be called from the UI thread.
        /// </summary>
        public async Task RunAsync()
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
            cts = new CancellationTokenSource();

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

            try
            {
                // Run the work on a background thread, pump messages on the UI thread
                var workTask = Task.Run(() => DoWork(cts.Token), cts.Token);

                // Pump UI messages until the worker finishes
                while (!workTask.IsCompleted)
                {
                    Application.DoEvents();
                    await Task.Delay(30);

                    // Check for user cancellation via the status dialog
                    if (!cts.IsCancellationRequested && !logger.ContinueWork())
                    {
                        cts.Cancel();
                    }
                }

                await workTask; // observe exceptions
            }
            catch (OperationCanceledException)
            {
                // Expected when user cancels
            }
            finally
            {
                logger.EndLogging();
                ShowCompletionMessage();
            }
        }

        private async Task DoWork(CancellationToken token)
        {
            FaviconDownloader.SetupTls();

            if (config.AllowSelfSignedCerts)
                FaviconDownloader.SetupSelfSignedCerts(true);

            try
            {
                IWebProxy proxy = Util.GetKeePassProxy();
                PwDatabase db = host.Database;

                int timeoutMs = config.Timeout * 1000;
                // Safety timeout: generous but not infinite
                int maxTotalWaitMs = Math.Max(timeoutMs * 5, entries.Length * 500);

                // Use a semaphore to limit concurrent downloads
                using (var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency))
                {
                    var tasks = new List<Task>();

                    for (int idx = 0; idx < entries.Length; idx++)
                    {
                        token.ThrowIfCancellationRequested();

                        PwEntry entry = entries[idx];

                        // Wait for a semaphore slot (with timeout to check cancellation)
                        while (!semaphore.Wait(500, token))
                        {
                            token.ThrowIfCancellationRequested();
                        }

                        token.ThrowIfCancellationRequested();

                        // Capture current index for closure
                        int entryIndex = idx;
                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                token.ThrowIfCancellationRequested();
                                await ProcessEntryAsync(entry, db, proxy, token);
                            }
                            catch (OperationCanceledException)
                            {
                                Interlocked.Increment(ref skippedCount);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref errorCount);
                                string title = "?";
                                string url = "?";
                                try { title = entry.Strings.ReadSafe(PwDefs.TitleField); } catch (Exception ex2) { Logger.Debug("ProcessEntry", ex2); }
                                try { url = entry.Strings.ReadSafe(PwDefs.UrlField); } catch (Exception ex2) { Logger.Debug("ProcessEntry", ex2); }
                                lock (errorLogLock)
                                {
                                    errorLog.Add(string.Format("[{0}] {1}: {2}", title, url, ex.ToString()));
                                }
                            }
                            finally
                            {
                                semaphore.Release();

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
                            }
                        }, token);

                        tasks.Add(task);

                        // Periodically check for cancellation and wait for some tasks to complete
                        // to avoid unbounded memory growth with huge entry lists
                        if (tasks.Count >= MaxConcurrency * 2)
                        {
                            var completed = await Task.WhenAny(tasks);
                            tasks.Remove(completed);
                            // Observe any exceptions
                            try { await completed; }
                            catch (OperationCanceledException) { }
                        }
                    }

                    // Wait for all remaining tasks with periodic UI updates
                    int totalWaitedMs = 0;
                    int waitIntervalMs = 1000;

                    while (tasks.Count > 0)
                    {
                        // Use Task.WhenAny with a timeout task for .NET Framework 4.8 compatibility
                        var timeoutTask = Task.Delay(waitIntervalMs, token);
                        var completed = await Task.WhenAny(tasks.Concat(new[] { timeoutTask }).ToArray());
                        
                        if (completed == timeoutTask)
                        {
                            totalWaitedMs += waitIntervalMs;
                            
                            if (!logger.ContinueWork())
                            {
                                cts?.Cancel();
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
                            continue;
                        }
                        
                        tasks.Remove(completed);
                        // Observe any exceptions
                        try { await completed; }
                        catch (OperationCanceledException) { }
                    }
                }
            }
            finally
            {
                if (config.AllowSelfSignedCerts)
                    FaviconDownloader.SetupSelfSignedCerts(false);
            }
        }

        private async Task ProcessEntryAsync(PwEntry entry, PwDatabase db, IWebProxy proxy, CancellationToken token)
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
                string testHost = Util.ExtractHost("https://" + url);
                if (!string.IsNullOrEmpty(testHost) && Util.IsPrivateHost(testHost))
                    url = "http://" + url;
                else
                    url = "https://" + url;
            }

            var downloader = new FaviconDownloader(config, proxy);
            FaviconResult result = downloader.Download(url, token);

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
                            catch (Exception ex) { Logger.Debug("ProcessEntry", ex); }
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
            catch (Exception ex) { Logger.Debug("InvokeOnUI", ex); }
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
                catch (Exception ex) { Logger.Error("ShowCompletionMessage", ex); }
            }

            bool wasCancelled = cts?.IsCancellationRequested ?? false;

            string message = string.Format(
                "KeeFetch completed.\n\n" +
                "Total entries: {0}\n" +
                "Icons downloaded: {1}\n" +
                "Skipped: {2}\n" +
                "Not found: {3}\n" +
                "Errors: {4}",
                totalCount, successCount, skippedCount, notFoundCount, errorCount);

            if (wasCancelled)
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
                catch (Exception ex) { Logger.Error("ShowCompletionMessage", ex); }
            }

            MessageBox.Show(message, "KeeFetch", MessageBoxButtons.OK,
                wasCancelled ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
