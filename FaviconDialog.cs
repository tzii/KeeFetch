using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeeFetch.Batch;
using KeeFetch.FetchProfiles;
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
        private readonly string profileId;
        private readonly ConcurrentQueue<PendingIconUpdate> pendingIconUpdates =
            new ConcurrentQueue<PendingIconUpdate>();
        private IStatusLogger logger;
        private CancellationTokenSource cts;

        private int totalCount;
        private int successCount;
        private int skippedCount;
        private int notFoundCount;
        private int errorCount;
        private int processedCount;
        private int pendingIconUpdateCount;
        private bool dbModified;
        private BatchEntryOutcome[] outcomes;
        private string lastProgressText;

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
            profileId = config.FetchProfileId;
            downloader = new FaviconDownloader(config);
        }

        /// <summary>
        /// Runs the download dialog asynchronously. Must be called from the UI thread.
        /// </summary>
        public BatchRunResult Result { get; private set; }

        public async Task<BatchRunResult> RunAsync()
        {
            ResetRunState();
            var runStopwatch = Stopwatch.StartNew();

            if (entries == null || entries.Length == 0)
            {
                Result = BuildBatchRunResult(
                    new PwEntry[0],
                    new BatchEntryOutcome[0],
                    false,
                    TimeSpan.Zero,
                    profileId,
                    null,
                    null);
                return Result;
            }

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
                    UpdateProgressDisplay(false);

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
                bool wasCancelled = cts != null && cts.IsCancellationRequested;
                CompleteMissingOutcomes(wasCancelled);
                ApplyDatabaseChanges();

                string diagnosticsLogPath;
                string diagnosticsCsvPath;
                ExportDiagnostics(out diagnosticsLogPath, out diagnosticsCsvPath);

                runStopwatch.Stop();
                Result = BuildBatchRunResult(
                    entries,
                    outcomes,
                    wasCancelled,
                    runStopwatch.Elapsed,
                    profileId,
                    diagnosticsLogPath,
                    diagnosticsCsvPath);

                UpdateProgressDisplay(true);
                FaviconDownloader.ClearCache();
                logger.EndLogging();
                cts.Dispose();
            }

            return Result;
        }

        private void ResetRunState()
        {
            totalCount = entries == null ? 0 : entries.Length;
            successCount = 0;
            skippedCount = 0;
            notFoundCount = 0;
            errorCount = 0;
            processedCount = 0;
            pendingIconUpdateCount = 0;
            dbModified = false;
            outcomes = new BatchEntryOutcome[totalCount];
            lastProgressText = null;
            Result = null;

            PendingIconUpdate ignored;
            while (pendingIconUpdates.TryDequeue(out ignored))
            {
            }

            lock (errorLogLock) { errorLog.Clear(); }
            lock (diagnosticsLogLock)
            {
                diagnosticsLog.Clear();
                diagnosticsCsvRows.Clear();
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
                    bool schedulingCancelled = false;
                    try
                    {
                        for (int idx = 0; idx < entries.Length; idx++)
                        {
                            token.ThrowIfCancellationRequested();
                            int entryIndex = idx;
                            PwEntry entry = entries[entryIndex];

                            while (!await semaphore.WaitAsync(500, token))
                            {
                                token.ThrowIfCancellationRequested();
                            }

                            var task = Task.Run(async () =>
                            {
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    await ProcessEntryAsync(entryIndex, entry, db, token);
                                }
                                catch (OperationCanceledException)
                                {
                                    TrySetOutcome(entryIndex, CreateOutcome(
                                        entry,
                                        null,
                                        BatchEntryStatus.Cancelled,
                                        null,
                                        false,
                                        "Cancelled while processing."));
                                }
                                catch (Exception ex)
                                {
                                    string url = ReadEntryValue(entry, PwDefs.UrlField, "?");
                                    AddError(entry, url, ex);
                                    TrySetOutcome(entryIndex, CreateOutcome(
                                        entry,
                                        url,
                                        BatchEntryStatus.RecoverableError,
                                        null,
                                        false,
                                        ex.Message));
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            });

                            tasks.Add(task);

                            if (tasks.Count >= MaxConcurrency * 2)
                            {
                                Task completed = await Task.WhenAny(tasks);
                                tasks.Remove(completed);
                                await completed;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        schedulingCancelled = true;
                    }

                    await Task.WhenAll(tasks);
                    if (schedulingCancelled)
                        token.ThrowIfCancellationRequested();
                }

                token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (config.AllowSelfSignedCerts)
                    FaviconDownloader.SetupSelfSignedCerts(false);
            }
        }

        private async Task ProcessEntryAsync(int entryIndex, PwEntry entry,
            PwDatabase db, CancellationToken token)
        {
            if (entry == null)
            {
                TrySetOutcome(entryIndex, CreateOutcome(
                    null,
                    null,
                    BatchEntryStatus.InvalidInput,
                    null,
                    false,
                    "Entry is null."));
                return;
            }

            if (config.SkipExistingIcons && !entry.CustomIconUuid.Equals(PwUuid.Zero))
            {
                TrySetOutcome(entryIndex, CreateOutcome(
                    entry,
                    null,
                    BatchEntryStatus.Skipped,
                    null,
                    false,
                    "Entry already has a custom icon."));
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
                TrySetOutcome(entryIndex, CreateOutcome(
                    entry,
                    url,
                    BatchEntryStatus.InvalidInput,
                    null,
                    false,
                    "Entry has no usable URL or title-derived domain."));
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            FaviconResult result = await downloader.DownloadAsync(url, token).ConfigureAwait(false);
            if (result.ElapsedMilliseconds <= 0)
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            if (result.Status != FaviconStatus.Success || result.IconData == null)
            {
                bool recoverable = IsRecoverableFailure(result);
                TrySetOutcome(entryIndex, CreateOutcome(
                    entry,
                    url,
                    recoverable ? BatchEntryStatus.RecoverableError : BatchEntryStatus.NotFound,
                    result,
                    recoverable,
                    string.IsNullOrWhiteSpace(result.DiagnosticsSummary)
                        ? "No usable icon was found."
                        : result.DiagnosticsSummary));
                AddDiagnosticsEntry(entry, url, result);
                return;
            }

            byte[] iconHash = Util.HashData(result.IconData);
            Interlocked.Increment(ref pendingIconUpdateCount);
            pendingIconUpdates.Enqueue(new PendingIconUpdate(entryIndex, entry, iconHash,
                result.IconData, result.Host, result.SelectedTier,
                result.WasSyntheticFallback, url, GetProviderId(result.Provider),
                result.ElapsedMilliseconds, result.DiagnosticsSummary));

            AddDiagnosticsEntry(entry, url, result);
        }

        private void FlushPendingUpdates(int maxBatchSize)
        {
            if (maxBatchSize <= 0)
                return;

            PwDatabase db = host.Database;
            if (db == null)
            {
                PendingIconUpdate unavailable;
                while (pendingIconUpdates.TryDequeue(out unavailable))
                {
                    Interlocked.Decrement(ref pendingIconUpdateCount);
                    TrySetOutcome(unavailable.EntryIndex, new BatchEntryOutcome(
                        unavailable.Entry,
                        unavailable.EntryTitle,
                        unavailable.ResolvedUrl,
                        BatchEntryStatus.RecoverableError,
                        unavailable.ProviderId,
                        unavailable.SelectedTier,
                        unavailable.WasSyntheticFallback,
                        false,
                        unavailable.ElapsedMilliseconds,
                        "KeePass database became unavailable before the icon could be applied."));
                }
                return;
            }

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
                        TrySetOutcome(pending.EntryIndex, new BatchEntryOutcome(
                            pending.Entry,
                            pending.EntryTitle,
                            pending.ResolvedUrl,
                            BatchEntryStatus.Updated,
                            pending.ProviderId,
                            pending.SelectedTier,
                            pending.WasSyntheticFallback,
                            false,
                            pending.ElapsedMilliseconds,
                            pending.Diagnostic));
                    }
                    catch (Exception ex)
                    {
                        AddError(pending.Entry, pending.ResolvedUrl, ex);
                        TrySetOutcome(pending.EntryIndex, new BatchEntryOutcome(
                            pending.Entry,
                            pending.EntryTitle,
                            pending.ResolvedUrl,
                            BatchEntryStatus.RecoverableError,
                            pending.ProviderId,
                            pending.SelectedTier,
                            pending.WasSyntheticFallback,
                            false,
                            pending.ElapsedMilliseconds,
                            ex.Message));
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

        }

        private void UpdateProgressDisplay(bool isComplete)
        {
            int currentProcessed = Interlocked.CompareExchange(ref processedCount, 0, 0);
            int pct = totalCount > 0 ? (int)(currentProcessed * 100.0 / totalCount) : 100;
            uint progressValue = (uint)Math.Min(Math.Max(pct, 0), 100);

            int currentSuccess = Interlocked.CompareExchange(ref successCount, 0, 0);
            int currentSkipped = Interlocked.CompareExchange(ref skippedCount, 0, 0);
            int currentNotFound = Interlocked.CompareExchange(ref notFoundCount, 0, 0);
            int currentErrors = Interlocked.CompareExchange(ref errorCount, 0, 0);
            int currentPending = Interlocked.CompareExchange(ref pendingIconUpdateCount, 0, 0);
            bool cancellationRequested = cts != null && cts.IsCancellationRequested;
            string detail = null;
            if (cancellationRequested && !isComplete)
                detail = "Waiting for active requests";
            else if (currentPending > 0)
                detail = "Applying downloaded icons";

            var snapshot = new BatchProgressSnapshot(
                totalCount,
                currentProcessed,
                currentSuccess,
                currentSkipped,
                currentNotFound,
                currentErrors,
                cancellationRequested,
                isComplete,
                detail);
            string progressText = BuildProgressText(snapshot);

            logger.SetProgress(progressValue);
            if (!string.Equals(lastProgressText, progressText, StringComparison.Ordinal))
            {
                lastProgressText = progressText;
                logger.SetText(progressText,
                    cancellationRequested ? LogStatusType.Warning : LogStatusType.Info);
            }
        }

        private void ApplyDatabaseChanges()
        {
            if (!dbModified)
                return;

            try
            {
                host.Database.UINeedsIconUpdate = true;
                host.MainWindow.UpdateUI(false, null, false, null, true, null, true);

                if (config.AutoSave && host.Database.IOConnectionInfo != null)
                    host.MainWindow.SaveDatabase(host.Database, null);
            }
            catch (Exception ex)
            {
                Logger.Error("ApplyDatabaseChanges", ex);
            }
        }

        private void ExportDiagnostics(out string diagnosticsLogPath,
            out string diagnosticsCsvPath)
        {
            diagnosticsLogPath = null;
            diagnosticsCsvPath = null;
            string logDir = null;
            try
            {
                string dbPath = host.Database.IOConnectionInfo.Path;
                if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                    logDir = Path.GetDirectoryName(dbPath);
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                logDir = Path.GetTempPath();

            List<string> logLines;
            List<string> csvRows;
            List<string> errors;
            lock (diagnosticsLogLock)
            {
                logLines = new List<string>(diagnosticsLog);
                csvRows = new List<string>(diagnosticsCsvRows);
            }
            lock (errorLogLock) { errors = new List<string>(errorLog); }

            if (errors.Count > 0)
            {
                if (logLines.Count > 0)
                    logLines.Add(string.Empty);
                logLines.Add("Processing errors:");
                logLines.AddRange(errors);
            }

            if (logLines.Count > 0)
            {
                try
                {
                    string path = Path.Combine(logDir, "KeeFetch_diagnostics.log");
                    File.WriteAllText(path, string.Join(Environment.NewLine, logLines));
                    diagnosticsLogPath = path;
                }
                catch (Exception ex)
                {
                    Logger.Error("ExportDiagnostics", ex);
                }
            }

            if (csvRows.Count > 0)
            {
                try
                {
                    string path = Path.Combine(logDir, "KeeFetch_diagnostics.csv");
                    var csvLines = new List<string>();
                    csvLines.Add(FaviconDiagnostics.BuildCsvHeader());
                    csvLines.AddRange(csvRows);
                    File.WriteAllText(path, string.Join(Environment.NewLine, csvLines));
                    diagnosticsCsvPath = path;
                }
                catch (Exception ex)
                {
                    Logger.Error("ExportDiagnostics", ex);
                }
            }
        }

        private void CompleteMissingOutcomes(bool wasCancelled)
        {
            for (int i = 0; i < outcomes.Length; i++)
            {
                if (outcomes[i] != null)
                    continue;

                TrySetOutcome(i, CreateOutcome(
                    entries[i],
                    null,
                    wasCancelled ? BatchEntryStatus.Cancelled : BatchEntryStatus.RecoverableError,
                    null,
                    false,
                    wasCancelled
                        ? "Cancelled before processing completed."
                        : "Processing ended without an outcome."));
            }
        }

        private bool TrySetOutcome(int entryIndex, BatchEntryOutcome outcome)
        {
            if (outcome == null || entryIndex < 0 || entryIndex >= outcomes.Length)
                return false;
            if (Interlocked.CompareExchange(ref outcomes[entryIndex], outcome, null) != null)
                return false;

            switch (outcome.Status)
            {
                case BatchEntryStatus.Updated:
                    Interlocked.Increment(ref successCount);
                    break;
                case BatchEntryStatus.Skipped:
                    Interlocked.Increment(ref skippedCount);
                    break;
                case BatchEntryStatus.NotFound:
                    Interlocked.Increment(ref notFoundCount);
                    break;
                case BatchEntryStatus.RecoverableError:
                case BatchEntryStatus.InvalidInput:
                    Interlocked.Increment(ref errorCount);
                    break;
            }

            if (outcome.Status != BatchEntryStatus.Cancelled)
                Interlocked.Increment(ref processedCount);
            return true;
        }

        internal static string BuildProgressText(BatchProgressSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            string prefix;
            if (snapshot.CancellationRequested && snapshot.IsComplete)
            {
                prefix = string.Format("Cancelled after {0} of {1}",
                    snapshot.ProcessedCount, snapshot.TotalCount);
            }
            else if (snapshot.CancellationRequested)
            {
                prefix = string.Format("Cancelling… Processed {0} of {1}",
                    snapshot.ProcessedCount, snapshot.TotalCount);
            }
            else
            {
                prefix = string.Format("Processed {0} of {1}",
                    snapshot.ProcessedCount, snapshot.TotalCount);
            }

            string text = string.Format(
                "{0} — updated {1}, skipped {2}, not found {3}, errors {4}",
                prefix,
                snapshot.UpdatedCount,
                snapshot.SkippedCount,
                snapshot.NotFoundCount,
                snapshot.ErrorCount);
            if (!string.IsNullOrWhiteSpace(snapshot.Detail))
                text += Environment.NewLine + snapshot.Detail;
            return text;
        }

        internal static BatchRunResult BuildBatchRunResult(
            PwEntry[] inputEntries,
            BatchEntryOutcome[] inputOutcomes,
            bool wasCancelled,
            TimeSpan elapsed,
            string profileId,
            string diagnosticsLogPath,
            string diagnosticsCsvPath)
        {
            PwEntry[] entrySnapshot = inputEntries ?? new PwEntry[0];
            BatchEntryOutcome[] outcomeSnapshot = inputOutcomes ??
                new BatchEntryOutcome[entrySnapshot.Length];
            if (entrySnapshot.Length != outcomeSnapshot.Length)
                throw new ArgumentException("Entries and outcomes must have the same length.");

            var ordered = new List<BatchEntryOutcome>(entrySnapshot.Length);
            for (int i = 0; i < entrySnapshot.Length; i++)
            {
                BatchEntryOutcome outcome = outcomeSnapshot[i];
                if (outcome == null)
                {
                    BatchEntryStatus status = wasCancelled
                        ? BatchEntryStatus.Cancelled
                        : BatchEntryStatus.RecoverableError;
                    outcome = new BatchEntryOutcome(
                        entrySnapshot[i],
                        ReadEntryValue(entrySnapshot[i], PwDefs.TitleField, string.Empty),
                        string.Empty,
                        status,
                        string.Empty,
                        IconTier.Rejected,
                        false,
                        false,
                        0,
                        wasCancelled
                            ? "Cancelled before processing completed."
                            : "Processing ended without an outcome.");
                }
                ordered.Add(outcome);
            }

            return new BatchRunResult(
                ordered,
                wasCancelled,
                elapsed,
                profileId,
                diagnosticsLogPath,
                diagnosticsCsvPath);
        }

        private static BatchEntryOutcome CreateOutcome(
            PwEntry entry,
            string resolvedUrl,
            BatchEntryStatus status,
            FaviconResult result,
            bool recoverable,
            string diagnostic)
        {
            return new BatchEntryOutcome(
                entry,
                ReadEntryValue(entry, PwDefs.TitleField, string.Empty),
                resolvedUrl ?? string.Empty,
                status,
                result != null ? GetProviderId(result.Provider) : string.Empty,
                result != null ? result.SelectedTier : IconTier.Rejected,
                result != null && result.WasSyntheticFallback,
                recoverable,
                result != null ? Math.Max(0L, result.ElapsedMilliseconds) : 0,
                diagnostic ?? string.Empty);
        }

        private static bool IsRecoverableFailure(FaviconResult result)
        {
            if (result == null)
                return false;

            if (result.ProviderMetrics != null && result.ProviderMetrics.Any(metric =>
                metric != null &&
                (ContainsIgnoreCase(metric.Outcome, "error") ||
                 ContainsIgnoreCase(metric.Outcome, "timeout"))))
            {
                return true;
            }

            return ContainsIgnoreCase(result.DiagnosticsSummary, "timeout") ||
                ContainsIgnoreCase(result.DiagnosticsSummary, "provider-error");
        }

        private void AddError(PwEntry entry, string resolvedUrl, Exception ex)
        {
            string title = ReadEntryValue(entry, PwDefs.TitleField, "?");
            lock (errorLogLock)
            {
                errorLog.Add(string.Format("[{0}] {1}: {2}",
                    title, resolvedUrl ?? string.Empty, ex.ToString()));
            }
        }

        private static string ReadEntryValue(PwEntry entry, string fieldName,
            string fallback)
        {
            if (entry == null)
                return fallback;

            try
            {
                return entry.Strings.ReadSafe(fieldName);
            }
            catch (Exception ex)
            {
                Logger.Debug("ReadEntryValue", ex);
                return fallback;
            }
        }

        private static bool ContainsIgnoreCase(string value, string fragment)
        {
            return value != null && value.IndexOf(
                fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetProviderId(string idOrDisplayName)
        {
            ProviderDefinition provider = FetchProfileCatalog.FindProvider(idOrDisplayName);
            return provider != null ? provider.Id : (idOrDisplayName ?? string.Empty);
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

        private sealed class PendingIconUpdate
        {
            public PendingIconUpdate(int entryIndex, PwEntry entry, byte[] iconHash,
                byte[] iconData, string iconHost, IconTier selectedTier,
                bool wasSyntheticFallback, string resolvedUrl, string providerId,
                long elapsedMilliseconds, string diagnostic)
            {
                EntryIndex = entryIndex;
                Entry = entry;
                IconHash = iconHash;
                IconData = iconData;
                IconHost = iconHost;
                SelectedTier = selectedTier;
                WasSyntheticFallback = wasSyntheticFallback;
                ResolvedUrl = resolvedUrl ?? string.Empty;
                EntryTitle = ReadEntryValue(entry, PwDefs.TitleField, string.Empty);
                ProviderId = providerId ?? string.Empty;
                ElapsedMilliseconds = Math.Max(0L, elapsedMilliseconds);
                Diagnostic = diagnostic ?? string.Empty;
            }

            public int EntryIndex { get; private set; }
            public PwEntry Entry { get; private set; }
            public byte[] IconHash { get; private set; }
            public byte[] IconData { get; private set; }
            public string IconHost { get; private set; }
            public IconTier SelectedTier { get; private set; }
            public bool WasSyntheticFallback { get; private set; }
            public string ResolvedUrl { get; private set; }
            public string EntryTitle { get; private set; }
            public string ProviderId { get; private set; }
            public long ElapsedMilliseconds { get; private set; }
            public string Diagnostic { get; private set; }
        }

    }
}
