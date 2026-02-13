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

            Form statusForm;
            logger = KeePass.UI.StatusUtil.CreateStatusDialog(
                host.MainWindow, out statusForm,
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
                // Run the work on a background thread
                var workTask = Task.Run(() => DoWork(cts.Token), cts.Token);

                // Pump UI messages until the worker finishes
                // Replaced Application.DoEvents() loop with simple await
                // The StatusDialog is modeless but we simulate modal behavior by waiting here
                while (!workTask.IsCompleted)
                {
                    // Check for user cancellation via the status dialog
                    if (!cts.IsCancellationRequested && !logger.ContinueWork())
                    {
                        cts.Cancel();
                        logger.SetText("Cancelling...", LogStatusType.Warning);
                    }

                    // Wait a bit to let the UI update (yielding to message loop)
                    await Task.Delay(100);
                }

                await workTask; // observe exceptions

                // Clear the download cache to free memory
                FaviconDownloader.ClearCache();
            }
            catch (OperationCanceledException)
            {
                // Expected when user cancels
            }
            finally
            {
                logger.EndLogging();
                ShowCompletionMessage();
                cts.Dispose();
            }
        }

        private async Task DoWork(CancellationToken token)
        {
            FaviconDownloader.SetupTls();

            if (config.AllowSelfSignedCerts)
                FaviconDownloader.SetupSelfSignedCerts(true);

            try
            {
                PwDatabase db = host.Database;

                int timeoutMs = config.Timeout * 1000;
                // Safety timeout: generous but not infinite
                int maxTotalWaitMs = Math.Max(timeoutMs * 5, entries.Length * 1000);

                // Use a semaphore to limit concurrent downloads
                using (var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency))
                {
                    var tasks = new List<Task>();

                    for (int idx = 0; idx < entries.Length; idx++)
                    {
                        token.ThrowIfCancellationRequested();

                        PwEntry entry = entries[idx];

                        // Wait for a semaphore slot (with timeout to check cancellation)
                        while (!await semaphore.WaitAsync(500, token))
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
                                await ProcessEntryAsync(entry, db, token);
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

                        // Periodically cleanup completed tasks
                        if (tasks.Count >= MaxConcurrency * 2)
                        {
                            var completed = await Task.WhenAny(tasks);
                            tasks.Remove(completed);
                            try { await completed; } catch (OperationCanceledException) { }
                        }
                    }

                    // Wait for all remaining tasks
                    await Task.WhenAll(tasks);
                }
            }
            finally
            {
                if (config.AllowSelfSignedCerts)
                    FaviconDownloader.SetupSelfSignedCerts(false);
            }
        }

        private async Task ProcessEntryAsync(PwEntry entry, PwDatabase db, CancellationToken token)
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

            // Using async DownloadAsync
            var downloader = new FaviconDownloader(config);
            FaviconResult result = await downloader.DownloadAsync(url, token).ConfigureAwait(false);

            if (result.Status != FaviconStatus.Success || result.IconData == null)
            {
                Interlocked.Increment(ref notFoundCount);
                return;
            }

            byte[] iconHash = Util.HashData(result.IconData);
            byte[] iconData = result.IconData;
            string iconHost = result.Host;

            // Marshal to UI thread - KeePass is single-threaded for DB operations
            await InvokeOnUIAsync(() =>
            {
                // Lock on the database to prevent race conditions
                // KeePass's PwDatabase is not thread-safe
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

        /// <summary>
        /// Invokes an action on the UI thread asynchronously with proper exception handling.
        /// Uses Invoke (not BeginInvoke) to ensure exceptions are properly propagated.
        /// </summary>
        private async Task InvokeOnUIAsync(Action action)
        {
            var mainForm = host.MainWindow;
            if (mainForm != null && mainForm.InvokeRequired)
            {
                // Use TaskCompletionSource to make Invoke async-await friendly
                var tcs = new TaskCompletionSource<object>();
                mainForm.Invoke(new Action(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                await tcs.Task;
            }
            else
            {
                // Already on UI thread, execute directly
                action();
            }
        }

        /// <summary>
        /// Invokes an action on the UI thread for progress updates (fire-and-forget).
        /// Exceptions are caught and logged.
        /// </summary>
        private void InvokeOnUI(Action action)
        {
            try
            {
                var mainForm = host.MainWindow;
                if (mainForm != null && mainForm.InvokeRequired)
                {
                    mainForm.BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex) { Logger.Debug("InvokeOnUI", ex); }
                    }));
                }
                else
                {
                    action();
                }
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

            bool wasCancelled = cts != null && cts.IsCancellationRequested;

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
                    string logDir = null;
                    try
                    {
                        var dbPath = host.Database.IOConnectionInfo.Path;
                        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                            logDir = Path.GetDirectoryName(dbPath);
                    }
                    catch { }

                    if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                        logDir = Path.GetTempPath();

                    string logPath = Path.Combine(logDir, "KeeFetch_errors.log");
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
