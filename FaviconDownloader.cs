using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KeeFetch.FetchProfiles;
using KeeFetch.IconProviders;
using KeeFetch.IconSelection;

namespace KeeFetch
{
    /// <summary>
    /// Collects provider candidates, ranks by tier/score, and selects the best favicon result.
    /// </summary>
    internal sealed class FaviconDownloader
    {
        internal static readonly string[] DefaultProviderOrder = new[]
        {
            "Direct Site",
            "Twenty Icons",
            "DuckDuckGo",
            "Google",
            "Yandex",
            "Favicone",
            "Icon Horse"
        };

        private static readonly Dictionary<string, Func<IIconProvider>> ProviderFactories =
            new Dictionary<string, Func<IIconProvider>>(StringComparer.OrdinalIgnoreCase)
            {
                { "direct-site", () => new DirectSiteProvider() },
                { "twenty-icons", () => new TwentyIconsProvider() },
                { "duckduckgo", () => new DuckDuckGoProvider() },
                { "google", () => new GoogleProvider() },
                { "yandex", () => new YandexProvider() },
                { "favicone", () => new FaviconeProvider() },
                { "icon-horse", () => new IconHorseProvider() }
            };

        private static readonly ConcurrentDictionary<string, CachedIconEntry> DownloadCache =
            new ConcurrentDictionary<string, CachedIconEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, CachedNegativeEntry> NegativeDownloadCache =
            new ConcurrentDictionary<string, CachedNegativeEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Lazy<Task<FaviconResult>>> InFlightDownloads =
            new ConcurrentDictionary<string, Lazy<Task<FaviconResult>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProviderSemaphores =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private readonly Configuration config;
        private readonly IconSelector selector = new IconSelector();
        private readonly FetchExecutionPolicy policy;
        private readonly ConcurrentDictionary<string, ProviderHealthState> providerHealth =
            new ConcurrentDictionary<string, ProviderHealthState>(StringComparer.OrdinalIgnoreCase);

        private const int MinProviderSliceMs = 1000;

        /// <summary>Configured cap for a single Google Play store lookup.</summary>
        private const int AndroidStoreLookupMaxMs = 7000;

        public FaviconDownloader(Configuration config)
        {
            this.config = config;
            this.policy = FetchExecutionPolicy.Resolve(config);
        }

        /// <summary>
        /// The single authoritative execution policy this instance runs with,
        /// resolved once from the managed catalog or the custom configuration.
        /// </summary>
        internal FetchExecutionPolicy ResolvedPolicy
        {
            get { return policy; }
        }

        /// <summary>
        /// Raises the per-host connection limit for concurrent icon downloads.
        /// TLS protocol selection is deliberately left at the .NET 4.8 default
        /// (SystemDefault), which follows OS policy and already negotiates TLS 1.2/1.3;
        /// forcing a protocol set here would silently re-enable TLS 1.0/1.1 for the
        /// whole KeePass process.
        /// </summary>
        public static void SetupTls()
        {
            try
            {
                if (ServicePointManager.DefaultConnectionLimit < 24)
                    ServicePointManager.DefaultConnectionLimit = 24;
                ServicePointManager.MaxServicePointIdleTime = 10000;
            }
            catch (Exception ex)
            {
                Logger.Warn("SetupTls", ex);
            }
        }

        /// <summary>
        /// Enables or disables acceptance of self-signed certificates for KeeFetch's
        /// own HTTP client only. Calls must be balanced.
        /// </summary>
        public static void SetupSelfSignedCerts(bool allow)
        {
            SharedHttp.SetAllowSelfSignedCertificates(allow);
        }

        public static byte[] GetCachedIcon(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            CachedIconEntry entry;
            DownloadCache.TryGetValue(cacheKey, out entry);
            return entry != null ? entry.IconData : null;
        }

        internal static CachedIconEntry GetCachedEntry(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            CachedIconEntry entry;
            DownloadCache.TryGetValue(cacheKey, out entry);
            return entry;
        }

        public static void CacheIcon(string cacheKey, byte[] iconData)
        {
            CacheIcon(cacheKey, iconData, "Cache", IconTier.SiteCanonical, false, "cache-hit");
        }

        internal static void CacheIcon(string cacheKey, byte[] iconData, string provider,
            IconTier selectedTier, bool wasSyntheticFallback, string diagnosticsSummary)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || iconData == null)
                return;

            DownloadCache[cacheKey] = new CachedIconEntry
            {
                IconData = iconData,
                Provider = string.IsNullOrWhiteSpace(provider) ? "Cache" : provider,
                SelectedTier = selectedTier,
                WasSyntheticFallback = wasSyntheticFallback,
                DiagnosticsSummary = string.IsNullOrWhiteSpace(diagnosticsSummary) ? "cache-hit" : diagnosticsSummary
            };
        }

        public static void ClearCache()
        {
            DownloadCache.Clear();
            NegativeDownloadCache.Clear();
            InFlightDownloads.Clear();
        }

        public async Task<FaviconResult> DownloadAsync(string url, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();

            int timeoutMs = policy.CumulativeTimeoutMs;
            int maxSize = config.MaxIconSize;

            if (AndroidAppMapper.IsAndroidUrl(url))
            {
                var androidResult = await DownloadAndroidIconAsync(url, maxSize, timeoutMs, token)
                    .ConfigureAwait(false);
                androidResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return androidResult;
            }

            Uri normalizedUri;
            if (!Util.TryParseHttpUri(url, config.PrefixUrls, out normalizedUri))
            {
                return new FaviconResult
                {
                    Status = FaviconStatus.NotFound,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            string host = normalizedUri.Host;
            string cacheKey = Util.GetNormalizedOriginKey(normalizedUri);
            bool isPrivate = Util.IsPrivateHost(host);

            var cached = GetCachedEntry(cacheKey);
            if (cached != null)
            {
                var cachedResult = BuildCachedResult(cached, host, cacheKey);
                cachedResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return cachedResult;
            }

            string inFlightKey = BuildInFlightKey(cacheKey, maxSize);
            CachedNegativeEntry negativeEntry;
            if (NegativeDownloadCache.TryGetValue(inFlightKey, out negativeEntry))
            {
                var negativeResult = BuildNegativeCachedResult(negativeEntry, host, cacheKey);
                negativeResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return negativeResult;
            }

            var lazyDownload = new Lazy<Task<FaviconResult>>(
                () => DownloadParsedHttpAsync(url, normalizedUri, host, cacheKey, isPrivate,
                    maxSize, timeoutMs, token),
                LazyThreadSafetyMode.ExecutionAndPublication);
            var activeDownload = InFlightDownloads.GetOrAdd(inFlightKey, lazyDownload);
            bool isOwner = ReferenceEquals(activeDownload, lazyDownload);

            FaviconResult result;
            try
            {
                result = await activeDownload.Value.ConfigureAwait(false);
            }
            finally
            {
                if (isOwner)
                {
                    Lazy<Task<FaviconResult>> ignored;
                    InFlightDownloads.TryRemove(inFlightKey, out ignored);
                }
            }

            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            if (isOwner && result.Status == FaviconStatus.NotFound)
                CacheNegativeResult(inFlightKey, result);

            return isOwner
                ? result
                : BuildCoalescedResult(result, stopwatch.ElapsedMilliseconds);
        }

        private async Task<FaviconResult> DownloadParsedHttpAsync(string originalUrl, Uri normalizedUri,
            string host, string cacheKey, bool isPrivate, int maxSize, int timeoutMs, CancellationToken token)
        {
            var request = new IconRequest
            {
                OriginalUrl = originalUrl,
                TargetHost = host,
                TargetOrigin = normalizedUri.GetLeftPart(UriPartial.Authority),
                CacheKey = cacheKey,
                MaxIconSize = maxSize,
                TimeoutMs = timeoutMs,
                AllowPrivateResponse = isPrivate
            };

            var collection = await CollectCandidatesAsync(request, isPrivate, token).ConfigureAwait(false);
            var selection = selector.Select(collection.Candidates, collection.AttemptedProviders,
                policy.AllowSyntheticFallbacks);
            var result = BuildResultFromSelection(selection, host, cacheKey, maxSize);
            result.ProviderMetrics = collection.ProviderMetrics;
            if (collection.CumulativeBudgetExhausted)
            {
                result.DiagnosticsSummary = string.IsNullOrWhiteSpace(result.DiagnosticsSummary)
                    ? "cumulative-budget-exhausted"
                    : result.DiagnosticsSummary + "; cumulative-budget-exhausted";
            }
            return result;
        }

        private async Task<FaviconResult> DownloadAndroidIconAsync(string url, int maxSize, int timeoutMs,
            CancellationToken token = default(CancellationToken))
        {
            // One outer clock bounds the complete Android request: the domain
            // resolver phase and any Google Play phase share the policy's single
            // cumulative budget instead of each receiving an independent budget.
            var requestStopwatch = Stopwatch.StartNew();

            var providerMetrics = new List<ProviderAttemptMetric>();
            string packageName = AndroidAppMapper.GetPackageName(url);
            string mappedDomain = AndroidAppMapper.MapToWebDomain(url);
            string guessedDomain = string.IsNullOrWhiteSpace(mappedDomain)
                ? AndroidAppMapper.TryGuessFromPackage(packageName)
                : null;

            string resolvedDomain = !string.IsNullOrWhiteSpace(mappedDomain)
                ? mappedDomain
                : guessedDomain;

            var combinedCandidates = new List<IconCandidate>();
            var attemptedProviders = new List<string>();
            bool androidStoreSkipped = false;

            string hostForResult = resolvedDomain ?? packageName;
            string cacheKey = null;

            if (!string.IsNullOrWhiteSpace(resolvedDomain))
            {
                Uri domainUri;
                if (Util.TryParseHttpUri("https://" + resolvedDomain, true, out domainUri))
                {
                    cacheKey = Util.GetNormalizedOriginKey(domainUri);
                    var cached = GetCachedEntry(cacheKey);
                    if (cached != null)
                    {
                        var cachedResult = BuildCachedResult(cached, resolvedDomain, cacheKey);
                        cachedResult.ProviderMetrics = new List<ProviderAttemptMetric>(cachedResult.ProviderMetrics);
                        return cachedResult;
                    }

                    var domainRequest = new IconRequest
                    {
                        OriginalUrl = url,
                        TargetHost = resolvedDomain,
                        TargetOrigin = domainUri.GetLeftPart(UriPartial.Authority),
                        CacheKey = cacheKey,
                        TargetPackageName = packageName,
                        MaxIconSize = maxSize,
                        TimeoutMs = timeoutMs,
                        AllowPrivateResponse = Util.IsPrivateHost(resolvedDomain)
                    };

                    var collected = await CollectCandidatesAsync(domainRequest,
                        Util.IsPrivateHost(resolvedDomain), token,
                        requestStopwatch.ElapsedMilliseconds).ConfigureAwait(false);
                    combinedCandidates.AddRange(collected.Candidates);
                    attemptedProviders.AddRange(collected.AttemptedProviders);
                    providerMetrics.AddRange(collected.ProviderMetrics);
                }
            }

            // The Google Play store lookup is a third-party network path and is
            // therefore gated by the explicit policy flag (Privacy: denied).
            if (!string.IsNullOrWhiteSpace(packageName) && policy.AllowAndroidStoreLookup)
            {
                token.ThrowIfCancellationRequested();

                long remainingMs = policy.CumulativeTimeoutMs - requestStopwatch.ElapsedMilliseconds;
                if (remainingMs >= MinProviderSliceMs)
                {
                    attemptedProviders.Add("Google Play");
                    var playStopwatch = Stopwatch.StartNew();
                    int playBudgetMs = (int)Math.Min(AndroidStoreLookupMaxMs, remainingMs);
                    var playCandidate = await AndroidAppMapper.FetchGooglePlayIconCandidateAsync(
                        packageName, playBudgetMs, token).ConfigureAwait(false);
                    providerMetrics.Add(new ProviderAttemptMetric("Google Play",
                        playStopwatch.ElapsedMilliseconds, playCandidate != null ? 1 : 0,
                        playCandidate != null ? "candidate" : "empty"));
                    if (playCandidate != null)
                    {
                        if (string.IsNullOrWhiteSpace(playCandidate.TargetHost))
                            playCandidate.TargetHost = hostForResult;
                        combinedCandidates.Add(playCandidate);
                    }
                }
                else
                {
                    androidStoreSkipped = true;
                    providerMetrics.Add(new ProviderAttemptMetric("Google Play", 0, 0,
                        "skipped-budget-exhausted"));
                }
            }

            if (string.IsNullOrWhiteSpace(cacheKey) && !string.IsNullOrWhiteSpace(packageName))
                cacheKey = "androidapp://" + packageName.ToLowerInvariant();

            var selection = selector.Select(combinedCandidates, attemptedProviders,
                policy.AllowSyntheticFallbacks);
            var result = BuildResultFromSelection(selection, hostForResult, cacheKey, maxSize);
            result.ProviderMetrics = providerMetrics;
            result.ElapsedMilliseconds = requestStopwatch.ElapsedMilliseconds;
            if (androidStoreSkipped)
            {
                result.DiagnosticsSummary = string.IsNullOrWhiteSpace(result.DiagnosticsSummary)
                    ? "android-store-skipped-budget-exhausted"
                    : result.DiagnosticsSummary + "; android-store-skipped-budget-exhausted";
            }
            return result;
        }

        /// <param name="preElapsedMs">
        /// Milliseconds of the policy cumulative budget already consumed by an
        /// outer phase (e.g. Android package resolution) before this chain runs.
        /// </param>
        private async Task<CandidateCollectionResult> CollectCandidatesAsync(IconRequest request,
            bool isPrivateTarget, CancellationToken token, long preElapsedMs = 0)
        {
            var result = new CandidateCollectionResult();
            var providers = BuildProviderPipeline(isPrivateTarget);
            if (providers.Count == 0)
                return result;

            var stopwatch = Stopwatch.StartNew();

            foreach (var provider in providers)
            {
                token.ThrowIfCancellationRequested();
                result.AttemptedProviders.Add(provider.Name);

                // The cumulative budget is a hard wall-clock ceiling over the
                // whole pipeline; exhaustion is surfaced explicitly instead of
                // silently degrading to not-found.
                int remaining = (int)Math.Max(0, policy.CumulativeTimeoutMs - preElapsedMs - stopwatch.ElapsedMilliseconds);
                if (remaining < MinProviderSliceMs)
                {
                    result.CumulativeBudgetExhausted = true;
                    break;
                }

                int budgetMs = IsPrimaryProvider(provider)
                    ? policy.PrimaryTimeoutMs
                    : policy.FallbackTimeoutMs;
                int deadlineMs = Math.Min(budgetMs, remaining);

                var providerRequest = CloneRequest(request, deadlineMs);

                IReadOnlyList<IconCandidate> candidates;
                var providerStopwatch = Stopwatch.StartNew();
                string providerOutcome = "empty";
                int candidateCount = 0;
                try
                {
                    // The linked deadline bounds the complete provider attempt,
                    // including any retries the provider performs internally.
                    using (var providerCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        providerCts.CancelAfter(deadlineMs);
                        try
                        {
                            candidates = await ExecuteProviderWithConcurrencyAsync(provider, providerRequest,
                                providerCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                                throw;
                            candidates = null;
                            providerOutcome = "timeout";
                        }
                        candidateCount = candidates != null ? candidates.Count : 0;
                        if (providerOutcome != "timeout")
                        {
                            // A provider may swallow the deadline abort and
                            // return no candidates; the fired deadline
                            // distinguishes a timeout from an ordinary empty
                            // result, while an externally cancelled token
                            // stays cancellation.
                            bool noResults = candidates == null || candidates.Count == 0;
                            if (noResults && providerCts.IsCancellationRequested)
                                providerOutcome = token.IsCancellationRequested ? "cancelled" : "timeout";
                            else
                                providerOutcome = candidateCount > 0 ? "candidate" : "empty";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    result.ProviderMetrics.Add(new ProviderAttemptMetric(provider.Name,
                        providerStopwatch.ElapsedMilliseconds, candidateCount, "cancelled"));
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn("CollectCandidatesAsync", ex);
                    RecordProviderFailure(provider.Name);
                    candidates = null;
                    providerOutcome = "error";
                }

                result.ProviderMetrics.Add(new ProviderAttemptMetric(provider.Name,
                    providerStopwatch.ElapsedMilliseconds, candidateCount, providerOutcome));

                if (providerOutcome == "timeout")
                {
                    RecordProviderFailure(provider.Name);
                    continue;
                }

                if (candidates != null && candidates.Count > 0)
                {
                    foreach (var candidate in candidates)
                    {
                        if (candidate == null)
                            continue;
                        result.Candidates.Add(candidate);
                    }
                    RecordProviderSuccess(provider.Name);

                    if (CanStopEarly(provider, result.Candidates))
                        break;
                }
            }

            if (result.CumulativeBudgetExhausted)
            {
                result.ProviderMetrics.Add(new ProviderAttemptMetric("Pipeline",
                    Math.Max(0, stopwatch.ElapsedMilliseconds), 0, "timeout"));
            }

            // External cancellation must propagate even when the last provider
            // swallowed its abort exception.
            token.ThrowIfCancellationRequested();

            return result;
        }

        private static bool IsPrimaryProvider(IIconProvider provider)
        {
            return provider.Capabilities.DefaultTier == IconTier.SiteCanonical &&
                   provider.Name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<IReadOnlyList<IconCandidate>> ExecuteProviderWithConcurrencyAsync(
            IIconProvider provider, IconRequest request, CancellationToken token)
        {
            var semaphore = ProviderSemaphores.GetOrAdd(provider.Name,
                _ => new SemaphoreSlim(provider.Capabilities.ConcurrencyCap,
                    provider.Capabilities.ConcurrencyCap));

            await semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await provider.GetCandidatesAsync(request, token).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private List<IIconProvider> BuildProviderPipeline(bool isPrivateTarget)
        {
            // The chain comes verbatim from the resolved execution policy; only
            // runtime context (private-target capability, provider cooldown)
            // may further restrict it.
            List<IIconProvider> orderedProviders = new List<IIconProvider>();

            foreach (string providerId in policy.ProviderIds)
            {
                Func<IIconProvider> factory;
                if (string.IsNullOrWhiteSpace(providerId) ||
                    !ProviderFactories.TryGetValue(providerId, out factory))
                {
                    // Defense in depth: a fingerprinted policy must never execute a
                    // partially resolved chain. Policy construction validates ids;
                    // reaching this point means an unvalidated policy escaped.
                    throw new InvalidOperationException(
                        "Execution policy references unknown provider id '" + providerId + "'.");
                }

                IIconProvider provider = factory();

                if (isPrivateTarget && !provider.Capabilities.AllowPrivateHosts)
                    continue;

                orderedProviders.Add(provider);
            }

            List<IIconProvider> active = new List<IIconProvider>();
            List<IIconProvider> cooledDown = new List<IIconProvider>();
            foreach (IIconProvider provider in orderedProviders)
            {
                if (IsProviderInCooldown(provider.Name))
                    cooledDown.Add(provider);
                else
                    active.Add(provider);
            }

            active.AddRange(cooledDown);
            return active;
        }

        private bool IsProviderInCooldown(string providerName)
        {
            ProviderHealthState state;
            if (!providerHealth.TryGetValue(providerName, out state))
                return false;

            return state.CooldownUntilUtc > DateTime.UtcNow;
        }

        private void RecordProviderSuccess(string providerName)
        {
            providerHealth.AddOrUpdate(providerName,
                _ => new ProviderHealthState(0, DateTime.MinValue),
                (_, __) => new ProviderHealthState(0, DateTime.MinValue));
        }

        private void RecordProviderFailure(string providerName)
        {
            providerHealth.AddOrUpdate(providerName,
                _ => new ProviderHealthState(1, DateTime.MinValue),
                (_, existing) =>
                {
                    int failures = existing.ConsecutiveFailures + 1;
                    DateTime cooldown = failures >= 4
                        ? DateTime.UtcNow.AddMinutes(1)
                        : existing.CooldownUntilUtc;
                    return new ProviderHealthState(failures, cooldown);
                });
        }

        private bool CanStopEarly(IIconProvider provider, IReadOnlyList<IconCandidate> candidates)
        {
            // The early-stop policy flag is the single authority: when it is off,
            // no provider result, however strong, may terminate the configured
            // chain; every remaining executable provider must still be queried.
            if (!policy.StopAfterStrongResolved)
                return false;

            if (provider == null || candidates == null || candidates.Count == 0)
                return false;

            // Safe fast path: if Direct Site already produced a strong non-blank raster icon,
            // there is little value in querying every resolver just to confirm it.
            if (provider.Name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase) &&
                candidates.Any(c => IsStrongStoppingCandidate(c, provider.Name, IconTier.SiteCanonical, 0.90)))
            {
                return true;
            }

            return candidates.Any(c => IsStrongStoppingCandidate(c, provider.Name, IconTier.StrongResolved, 0.72));
        }

        private static bool IsStrongStoppingCandidate(IconCandidate candidate, string providerName,
            IconTier minimumTier, double minimumConfidence)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.ProviderName))
                return false;

            if (!candidate.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (candidate.Tier > minimumTier)
                return false;

            return !candidate.IsSvg &&
                   !candidate.IsBlankSuspected &&
                   !candidate.IsPlaceholderSuspected &&
                   !candidate.IsSynthetic &&
                   candidate.ConfidenceScore >= minimumConfidence;
        }

        private static IconRequest CloneRequest(IconRequest source, int timeoutMs)
        {
            return new IconRequest
            {
                OriginalUrl = source.OriginalUrl,
                TargetHost = source.TargetHost,
                TargetOrigin = source.TargetOrigin,
                CacheKey = source.CacheKey,
                TargetPackageName = source.TargetPackageName,
                MaxIconSize = source.MaxIconSize,
                TimeoutMs = timeoutMs,
                AllowPrivateResponse = source.AllowPrivateResponse
            };
        }

        private FaviconResult BuildResultFromSelection(IconSelectionResult selection, string host,
            string cacheKey, int maxSize)
        {
            var result = new FaviconResult
            {
                Host = host,
                CacheKey = cacheKey,
                Selection = selection,
                DiagnosticsSummary = selection.DiagnosticsSummary,
                AttemptedProviders = selection.AttemptedProviders,
                RejectedCandidates = selection.RejectedCandidates
            };

            if (selection.SelectedCandidate == null)
            {
                result.Status = FaviconStatus.NotFound;
                result.Provider = null;
                result.SelectedTier = IconTier.Rejected;
                result.WasSyntheticFallback = false;
                return result;
            }

            byte[] selectedBytes = selection.SelectedCandidate.NormalizedPngData ??
                                   selection.SelectedCandidate.RawData;
            if (selectedBytes == null || !Util.IsValidImage(selectedBytes))
            {
                result.Status = FaviconStatus.NotFound;
                result.Provider = null;
                result.SelectedTier = IconTier.Rejected;
                result.WasSyntheticFallback = false;
                return result;
            }

            byte[] resized = Util.ResizeImage(selectedBytes, maxSize, maxSize);
            if (resized == null || !Util.IsValidImage(resized))
            {
                result.Status = FaviconStatus.NotFound;
                result.Provider = null;
                result.SelectedTier = IconTier.Rejected;
                result.WasSyntheticFallback = false;
                return result;
            }

            result.IconData = resized;
            result.Status = FaviconStatus.Success;
            result.Provider = selection.SelectedCandidate.ProviderName;
            result.SelectedTier = selection.SelectedCandidate.Tier;
            result.WasSyntheticFallback = selection.WasSyntheticFallback;

            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                CacheIcon(cacheKey, resized, result.Provider, result.SelectedTier,
                    result.WasSyntheticFallback, result.DiagnosticsSummary);
            }

            return result;
        }

        private static FaviconResult BuildCachedResult(CachedIconEntry cached, string host, string cacheKey)
        {
            return new FaviconResult
            {
                IconData = cached.IconData,
                Status = FaviconStatus.Success,
                Provider = cached.Provider,
                Host = host,
                CacheKey = cacheKey,
                SelectedTier = cached.SelectedTier,
                WasSyntheticFallback = cached.WasSyntheticFallback,
                ProviderMetrics = new List<ProviderAttemptMetric>
                {
                    new ProviderAttemptMetric("Cache", 0, 1, "hit")
                },
                DiagnosticsSummary = string.IsNullOrWhiteSpace(cached.DiagnosticsSummary)
                    ? "cache-hit"
                    : "cache-hit; " + cached.DiagnosticsSummary
            };
        }

        private static void CacheNegativeResult(string negativeKey, FaviconResult result)
        {
            if (string.IsNullOrWhiteSpace(negativeKey) || result == null ||
                result.Status != FaviconStatus.NotFound)
                return;

            NegativeDownloadCache[negativeKey] = new CachedNegativeEntry
            {
                DiagnosticsSummary = result.DiagnosticsSummary,
                AttemptedProviders = result.AttemptedProviders != null
                    ? result.AttemptedProviders.ToList()
                    : new List<string>(),
                RejectedCandidates = result.RejectedCandidates != null
                    ? result.RejectedCandidates.ToList()
                    : new List<IconCandidate>()
            };
        }

        private static FaviconResult BuildNegativeCachedResult(CachedNegativeEntry cached,
            string host, string cacheKey)
        {
            return new FaviconResult
            {
                Status = FaviconStatus.NotFound,
                Provider = null,
                Host = host,
                CacheKey = cacheKey,
                SelectedTier = IconTier.Rejected,
                WasSyntheticFallback = false,
                AttemptedProviders = cached.AttemptedProviders,
                RejectedCandidates = cached.RejectedCandidates,
                ProviderMetrics = new List<ProviderAttemptMetric>
                {
                    new ProviderAttemptMetric("Cache", 0, 0, "negative-hit")
                },
                DiagnosticsSummary = string.IsNullOrWhiteSpace(cached.DiagnosticsSummary)
                    ? "negative-cache-hit"
                    : "negative-cache-hit; " + cached.DiagnosticsSummary
            };
        }

        private string BuildInFlightKey(string cacheKey, int maxSize)
        {
            return string.Join("|", new[]
            {
                cacheKey ?? string.Empty,
                maxSize.ToString(),
                policy.Fingerprint()
            });
        }

        private static FaviconResult BuildCoalescedResult(FaviconResult source, long elapsedMilliseconds)
        {
            if (source == null)
            {
                return new FaviconResult
                {
                    Status = FaviconStatus.NotFound,
                    ElapsedMilliseconds = elapsedMilliseconds,
                    ProviderMetrics = new List<ProviderAttemptMetric>
                    {
                        new ProviderAttemptMetric("Coalesced", 0, 0, "empty")
                    },
                    DiagnosticsSummary = "coalesced-empty"
                };
            }

            string diagnostics = string.IsNullOrWhiteSpace(source.DiagnosticsSummary)
                ? "coalesced"
                : "coalesced; " + source.DiagnosticsSummary;

            return new FaviconResult
            {
                IconData = source.IconData,
                Status = source.Status,
                Provider = source.Provider,
                Host = source.Host,
                CacheKey = source.CacheKey,
                SelectedTier = source.SelectedTier,
                WasSyntheticFallback = source.WasSyntheticFallback,
                DiagnosticsSummary = diagnostics,
                AttemptedProviders = source.AttemptedProviders,
                RejectedCandidates = source.RejectedCandidates,
                Selection = source.Selection,
                ElapsedMilliseconds = elapsedMilliseconds,
                ProviderMetrics = new List<ProviderAttemptMetric>
                {
                    new ProviderAttemptMetric("Coalesced", 0,
                        source.Status == FaviconStatus.Success ? 1 : 0, "shared")
                }
            };
        }

        private sealed class ProviderHealthState
        {
            public ProviderHealthState(int consecutiveFailures, DateTime cooldownUntilUtc)
            {
                ConsecutiveFailures = consecutiveFailures;
                CooldownUntilUtc = cooldownUntilUtc;
            }

            public int ConsecutiveFailures { get; private set; }
            public DateTime CooldownUntilUtc { get; private set; }
        }

        private sealed class CandidateCollectionResult
        {
            public CandidateCollectionResult()
            {
                Candidates = new List<IconCandidate>();
                AttemptedProviders = new List<string>();
                ProviderMetrics = new List<ProviderAttemptMetric>();
            }

            public List<IconCandidate> Candidates { get; private set; }
            public List<string> AttemptedProviders { get; private set; }
            public List<ProviderAttemptMetric> ProviderMetrics { get; private set; }
            public bool CumulativeBudgetExhausted { get; set; }
        }

        internal sealed class CachedIconEntry
        {
            public byte[] IconData { get; set; }
            public string Provider { get; set; }
            public IconTier SelectedTier { get; set; }
            public bool WasSyntheticFallback { get; set; }
            public string DiagnosticsSummary { get; set; }
        }

        private sealed class CachedNegativeEntry
        {
            public string DiagnosticsSummary { get; set; }
            public IReadOnlyList<string> AttemptedProviders { get; set; }
            public IReadOnlyList<IconCandidate> RejectedCandidates { get; set; }
        }
    }

    internal enum FaviconStatus
    {
        Success,
        NotFound
    }

    internal sealed class FaviconResult
    {
        public FaviconResult()
        {
            Status = FaviconStatus.NotFound;
            AttemptedProviders = new List<string>();
            RejectedCandidates = new List<IconCandidate>();
            ProviderMetrics = new List<ProviderAttemptMetric>();
        }

        public byte[] IconData { get; set; }
        public FaviconStatus Status { get; set; }
        public string Provider { get; set; }
        public string Host { get; set; }
        public string CacheKey { get; set; }
        public IconTier SelectedTier { get; set; }
        public bool WasSyntheticFallback { get; set; }
        public string DiagnosticsSummary { get; set; }
        public IReadOnlyList<string> AttemptedProviders { get; set; }
        public IReadOnlyList<IconCandidate> RejectedCandidates { get; set; }
        public IReadOnlyList<ProviderAttemptMetric> ProviderMetrics { get; set; }
        public IconSelectionResult Selection { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }

    internal sealed class ProviderAttemptMetric
    {
        public ProviderAttemptMetric(string providerName, long elapsedMilliseconds,
            int candidateCount, string outcome)
        {
            ProviderName = providerName ?? string.Empty;
            ElapsedMilliseconds = elapsedMilliseconds;
            CandidateCount = candidateCount;
            Outcome = outcome ?? string.Empty;
        }

        public string ProviderName { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        public int CandidateCount { get; private set; }
        public string Outcome { get; private set; }
    }
}
