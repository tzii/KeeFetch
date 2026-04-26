using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Interfaces;
using KeeFetch.IconSelection;

namespace KeeFetch
{
    /// <summary>
    /// Manages concurrent favicon downloads with progress reporting and cancellation support.
    /// Network work runs off the UI thread; KeePass DB mutations are applied in small UI batches.
    /// </summary>
    internal sealed class FaviconDialog
    {
        private readonly IPluginHost host;
        private readonly Configuration config;
        private readonly PwEntry[] entries;
        private readonly FaviconDownloader downloader;
        private readonly ConcurrentQueue<PendingIconUpdate> pendingIconUpdates =
            new ConcurrentQueue<PendingIconUpdate>();
        private readonly Dictionary<string, ProviderMetricAggregate> providerMetricAggregates =
            new Dictionary<string, ProviderMetricAggregate>(StringComparer.OrdinalIgnoreCase);
        private readonly object providerMetricsLock = new object();

        private IStatusLogger logger;
        private CancellationTokenSource cts;

        private int totalCount;
        private int successCount;
        private int directSiteSuccessCount;
        private int resolvedSuccessCount;
        private int syntheticSuccessCount;
        private int skippedCount;
        private int notFoundCount;
        private int errorCount;
        private int processedCount;
        private int pendingIconUpdateCount;
        private int cacheHitCount;
        private bool dbModified;
        private long totalDownloadElapsedMs;
        private long maxDownloadElapsedMs;

        private readonly List<string> errorLog = new List<string>();
        private readonly List<string> diagnosticsLog = new List<string>();
        private readonly List<string> diagnosticsCsvRows = new List<string>();
        private readonly object errorLogLock = new object();
        private readonly object diagnosticsLogLock = new object();

        // Concurrency limit to avoid ThreadPool starvation and excessive network load.
        private const int MaxConcurrency = 8;
        private const int UiPollDelayMs = 100;
        private const int UiApplyBatchSize = 12;

        public FaviconDialog(IPluginHost host, Configuration config, PwEntry[] entries)
        {
            this.host = host;
            this.config = config;
            this.entries = entries;
            downloader = new FaviconDownloader(config);
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
            directSiteSuccessCount = 0;
            resolvedSuccessCount = 0;
            syntheticSuccessCount = 0;
            skippedCount = 0;
            notFoundCount = 0;
            errorCount = 0;
            processedCount = 0;
            pendingIconUpdateCount = 0;
            cacheHitCount = 0;
            dbModified = false;
            totalDownloadElapsedMs = 0;
            maxDownloadElapsedMs = 0;

            lock (errorLogLock) { errorLog.Clear(); }
            lock (diagnosticsLogLock)
            {
                diagnosticsLog.Clear();
                diagnosticsCsvRows.Clear();
            }
            lock (providerMetricsLock) { providerMetricAggregates.Clear(); }

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
                var workTask = Task.Run(() => DoWork(cts.Token), cts.Token);

                while (!workTask.IsCompleted)
                {
                    if (!cts.IsCancellationRequested && !logger.ContinueWork())
                    {
                        cts.Cancel();
                        logger.SetText("Cancelling...", LogStatusType.Warning);
                    }

                    FlushPendingUpdates(UiApplyBatchSize);
                    UpdateProgressDisplay();

                    await Task.Delay(UiPollDelayMs);
                }

                await workTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when the user cancels.
            }
            finally
            {
                FlushPendingUpdates(int.MaxValue);
                UpdateProgressDisplay();
                FaviconDownloader.ClearCache();
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

                using (var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency))
                {
                    var tasks = new List<Task>();

                    for (int idx = 0; idx < entries.Length; idx++)
                    {
                        token.ThrowIfCancellationRequested();

                        PwEntry entry = entries[idx];

                        while (!await semaphore.WaitAsync(500, token))
                        {
                            token.ThrowIfCancellationRequested();
                        }

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
                                try { title = entry.Strings.ReadSafe(PwDefs.TitleField); }
                                catch (Exception ex2) { Logger.Debug("ProcessEntry", ex2); }
                                try { url = entry.Strings.ReadSafe(PwDefs.UrlField); }
                                catch (Exception ex2) { Logger.Debug("ProcessEntry", ex2); }

                                lock (errorLogLock)
                                {
                                    errorLog.Add(string.Format("[{0}] {1}: {2}",
                                        title, url, ex.ToString()));
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                                Interlocked.Increment(ref processedCount);
                            }
                        }, token);

                        tasks.Add(task);

                        if (tasks.Count >= MaxConcurrency * 2)
                        {
                            var completed = await Task.WhenAny(tasks);
                            tasks.Remove(completed);
                            try { await completed; }
                            catch (OperationCanceledException) { }
                        }
                    }

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

            var stopwatch = Stopwatch.StartNew();
            FaviconResult result = await downloader.DownloadAsync(url, token).ConfigureAwait(false);
            if (result.ElapsedMilliseconds <= 0)
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            RecordDownloadMetrics(result);

            if (result.Status != FaviconStatus.Success || result.IconData == null)
            {
                Interlocked.Increment(ref notFoundCount);
                AddDiagnosticsEntry(entry, url, result);
                return;
            }

            byte[] iconHash = Util.HashData(result.IconData);
            pendingIconUpdates.Enqueue(new PendingIconUpdate(entry, iconHash, result.IconData,
                result.Host, result.SelectedTier, result.WasSyntheticFallback, url));
            Interlocked.Increment(ref pendingIconUpdateCount);

            AddDiagnosticsEntry(entry, url, result);
        }

        private void FlushPendingUpdates(int maxBatchSize)
        {
            if (maxBatchSize <= 0)
                return;

            PwDatabase db = host.Database;
            if (db == null)
                return;

            int applied = 0;

            lock (db)
            {
                while (applied < maxBatchSize)
                {
                    PendingIconUpdate pending;
                    if (!pendingIconUpdates.TryDequeue(out pending))
                        break;

                    Interlocked.Decrement(ref pendingIconUpdateCount);

                    try
                    {
                        ApplyIconUpdate(db, pending);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errorCount);
                        lock (errorLogLock)
                        {
                            errorLog.Add(string.Format("[{0}] {1}: {2}",
                                pending.EntryTitle, pending.ResolvedUrl, ex.ToString()));
                        }
                    }

                    applied++;
                }
            }
        }

        private void ApplyIconUpdate(PwDatabase db, PendingIconUpdate pending)
        {
            PwUuid iconUuid = new PwUuid(pending.IconHash);
            bool iconExists = db.CustomIcons.Any(ci => ci.Uuid.Equals(iconUuid));
            if (!iconExists)
            {
                PwCustomIcon newIcon = new PwCustomIcon(iconUuid, pending.IconData);

                string iconName = config.IconNamePrefix;
                if (!string.IsNullOrEmpty(iconName) && !string.IsNullOrEmpty(pending.IconHost))
                    iconName += pending.IconHost;
                else if (!string.IsNullOrEmpty(pending.IconHost))
                    iconName = pending.IconHost;

                if (!string.IsNullOrEmpty(iconName))
                {
                    try
                    {
                        var nameProperty = newIcon.GetType().GetProperty("Name");
                        if (nameProperty != null)
                            nameProperty.SetValue(newIcon, iconName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug("ApplyIconUpdate", ex);
                    }
                }

                db.CustomIcons.Add(newIcon);
            }

            if (!pending.Entry.CustomIconUuid.Equals(iconUuid))
            {
                pending.Entry.CustomIconUuid = iconUuid;
                pending.Entry.Touch(true, false);
                dbModified = true;
            }

            Interlocked.Increment(ref successCount);
            if (pending.WasSyntheticFallback || pending.SelectedTier == IconTier.SyntheticFallback)
                Interlocked.Increment(ref syntheticSuccessCount);
            else if (pending.SelectedTier == IconTier.SiteCanonical)
                Interlocked.Increment(ref directSiteSuccessCount);
            else if (pending.SelectedTier == IconTier.StrongResolved)
                Interlocked.Increment(ref resolvedSuccessCount);
        }

        private void UpdateProgressDisplay()
        {
            int currentProcessed = Interlocked.CompareExchange(ref processedCount, 0, 0);
            int pct = totalCount > 0 ? (int)(currentProcessed * 100.0 / totalCount) : 100;
            uint progressValue = (uint)Math.Min(Math.Max(pct, 0), 100);

            int currentSuccess = Interlocked.CompareExchange(ref successCount, 0, 0);
            int currentDirect = Interlocked.CompareExchange(ref directSiteSuccessCount, 0, 0);
            int currentResolved = Interlocked.CompareExchange(ref resolvedSuccessCount, 0, 0);
            int currentSynthetic = Interlocked.CompareExchange(ref syntheticSuccessCount, 0, 0);
            int currentSkipped = Interlocked.CompareExchange(ref skippedCount, 0, 0);
            int currentNotFound = Interlocked.CompareExchange(ref notFoundCount, 0, 0);
            int currentErrors = Interlocked.CompareExchange(ref errorCount, 0, 0);
            int currentPending = Interlocked.CompareExchange(ref pendingIconUpdateCount, 0, 0);

            logger.SetProgress(progressValue);
            logger.SetText(string.Format(
                "Processed {0}/{1} ({2}%) — OK: {3} (Direct {4}, Resolved {5}, Synthetic {6}), Skipped: {7}, Not found: {8}, Errors: {9}, Pending UI apply: {10}",
                currentProcessed, totalCount, pct,
                currentSuccess, currentDirect, currentResolved, currentSynthetic,
                currentSkipped, currentNotFound, currentErrors, currentPending),
                LogStatusType.Info);
        }

        private void RecordDownloadMetrics(FaviconResult result)
        {
            if (result == null)
                return;

            long elapsed = Math.Max(0L, result.ElapsedMilliseconds);
            Interlocked.Add(ref totalDownloadElapsedMs, elapsed);
            TryRecordMaxElapsed(elapsed);

            if (result.DiagnosticsSummary != null &&
                result.DiagnosticsSummary.IndexOf("cache-hit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Interlocked.Increment(ref cacheHitCount);
            }

            if (result.ProviderMetrics == null)
                return;

            lock (providerMetricsLock)
            {
                foreach (ProviderAttemptMetric metric in result.ProviderMetrics)
                {
                    if (metric == null || string.IsNullOrWhiteSpace(metric.ProviderName))
                        continue;

                    ProviderMetricAggregate aggregate;
                    if (!providerMetricAggregates.TryGetValue(metric.ProviderName, out aggregate))
                    {
                        aggregate = new ProviderMetricAggregate(metric.ProviderName);
                        providerMetricAggregates[metric.ProviderName] = aggregate;
                    }

                    aggregate.Add(metric);
                }
            }
        }

        private void TryRecordMaxElapsed(long elapsedMilliseconds)
        {
            while (true)
            {
                long current = Interlocked.Read(ref maxDownloadElapsedMs);
                if (elapsedMilliseconds <= current)
                    return;

                if (Interlocked.CompareExchange(ref maxDownloadElapsedMs,
                    elapsedMilliseconds, current) == current)
                    return;
            }
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
                catch (Exception ex)
                {
                    Logger.Error("ShowCompletionMessage", ex);
                }
            }

            bool wasCancelled = cts != null && cts.IsCancellationRequested;

            string message = string.Format(
                "KeeFetch completed.\n\n" +
                "Total entries: {0}\n" +
                "Icons downloaded: {1}\n" +
                "  - Direct-site successes: {2}\n" +
                "  - Third-party resolved successes: {3}\n" +
                "  - Synthetic fallback successes: {4}\n" +
                "Skipped: {5}\n" +
                "Not found: {6}\n" +
                "Errors: {7}",
                totalCount, successCount, directSiteSuccessCount, resolvedSuccessCount,
                syntheticSuccessCount, skippedCount, notFoundCount, errorCount);

            string timingSummary = BuildTimingSummary();
            if (!string.IsNullOrEmpty(timingSummary))
                message += "\n\n" + timingSummary;

            if (wasCancelled)
                message += "\n\nDownload was cancelled by user.";

            string logDir = null;
            try
            {
                var dbPath = host.Database.IOConnectionInfo.Path;
                if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                    logDir = Path.GetDirectoryName(dbPath);
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                logDir = Path.GetTempPath();

            if (diagnosticsLog.Count > 0)
            {
                try
                {
                    string diagnosticsPath = Path.Combine(logDir, "KeeFetch_diagnostics.log");
                    File.WriteAllText(diagnosticsPath, string.Join(Environment.NewLine, diagnosticsLog));
                    message += "\n\nDiagnostics log saved to:\n" + diagnosticsPath;

                    if (diagnosticsCsvRows.Count > 0)
                    {
                        string diagnosticsCsvPath = Path.Combine(logDir, "KeeFetch_diagnostics.csv");
                        var csvLines = new List<string>();
                        csvLines.Add(FaviconDiagnostics.BuildCsvHeader());
                        csvLines.AddRange(diagnosticsCsvRows);
                        File.WriteAllText(diagnosticsCsvPath, string.Join(Environment.NewLine, csvLines));
                        message += "\nDiagnostics CSV saved to:\n" + diagnosticsCsvPath;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ShowCompletionMessage", ex);
                }
            }

            if (errorLog.Count > 0)
            {
                try
                {
                    string logPath = Path.Combine(logDir, "KeeFetch_errors.log");
                    File.WriteAllText(logPath,
                        string.Join(Environment.NewLine + Environment.NewLine, errorLog));
                    message += "\n\nError log saved to:\n" + logPath;
                }
                catch (Exception ex)
                {
                    Logger.Error("ShowCompletionMessage", ex);
                }
            }

            MessageBox.Show(message, "KeeFetch", MessageBoxButtons.OK,
                wasCancelled ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void AddDiagnosticsEntry(PwEntry entry, string resolvedUrl, FaviconResult result)
        {
            try
            {
                string title = entry != null ? entry.Strings.ReadSafe(PwDefs.TitleField) : string.Empty;

                lock (diagnosticsLogLock)
                {
                    diagnosticsLog.Add(FaviconDiagnostics.BuildLogLine(title, resolvedUrl, result));
                    diagnosticsCsvRows.Add(FaviconDiagnostics.BuildCsvRow(title, resolvedUrl, result));
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("AddDiagnosticsEntry", ex);
            }
        }

        private string BuildTimingSummary()
        {
            int completed = Math.Max(1, Interlocked.CompareExchange(ref processedCount, 0, 0));
            long totalMs = Interlocked.Read(ref totalDownloadElapsedMs);
            long maxMs = Interlocked.Read(ref maxDownloadElapsedMs);
            int cacheHits = Interlocked.CompareExchange(ref cacheHitCount, 0, 0);
            long averageMs = completed > 0 ? totalMs / completed : 0;

            var lines = new List<string>();
            lines.Add(string.Format(
                "Timing: avg {0} ms per entry, slowest {1} ms, cache hits {2}.",
                averageMs, maxMs, cacheHits));

            List<ProviderMetricAggregate> aggregates;
            lock (providerMetricsLock)
            {
                aggregates = providerMetricAggregates.Values
                    .OrderByDescending(v => v.TotalElapsedMilliseconds)
                    .ThenBy(v => v.ProviderName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (aggregates.Count > 0)
            {
                lines.Add("Provider timings:");
                foreach (ProviderMetricAggregate aggregate in aggregates.Take(6))
                {
                    lines.Add(string.Format(
                        "- {0}: {1} calls, {2} ms total, avg {3} ms, candidates {4}, errors {5}",
                        aggregate.ProviderName,
                        aggregate.CallCount,
                        aggregate.TotalElapsedMilliseconds,
                        aggregate.CallCount > 0 ? aggregate.TotalElapsedMilliseconds / aggregate.CallCount : 0,
                        aggregate.TotalCandidates,
                        aggregate.ErrorCount));
                }
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private sealed class PendingIconUpdate
        {
            public PendingIconUpdate(PwEntry entry, byte[] iconHash, byte[] iconData,
                string iconHost, IconTier selectedTier, bool wasSyntheticFallback,
                string resolvedUrl)
            {
                Entry = entry;
                IconHash = iconHash;
                IconData = iconData;
                IconHost = iconHost;
                SelectedTier = selectedTier;
                WasSyntheticFallback = wasSyntheticFallback;
                ResolvedUrl = resolvedUrl ?? string.Empty;
                EntryTitle = entry != null ? entry.Strings.ReadSafe(PwDefs.TitleField) : string.Empty;
            }

            public PwEntry Entry { get; private set; }
            public byte[] IconHash { get; private set; }
            public byte[] IconData { get; private set; }
            public string IconHost { get; private set; }
            public IconTier SelectedTier { get; private set; }
            public bool WasSyntheticFallback { get; private set; }
            public string ResolvedUrl { get; private set; }
            public string EntryTitle { get; private set; }
        }

        private sealed class ProviderMetricAggregate
        {
            public ProviderMetricAggregate(string providerName)
            {
                ProviderName = providerName;
            }

            public string ProviderName { get; private set; }
            public int CallCount { get; private set; }
            public long TotalElapsedMilliseconds { get; private set; }
            public int TotalCandidates { get; private set; }
            public int ErrorCount { get; private set; }

            public void Add(ProviderAttemptMetric metric)
            {
                CallCount++;
                TotalElapsedMilliseconds += Math.Max(0L, metric.ElapsedMilliseconds);
                TotalCandidates += Math.Max(0, metric.CandidateCount);
                if (string.Equals(metric.Outcome, "error", StringComparison.OrdinalIgnoreCase))
                    ErrorCount++;
            }
        }
    }
}
