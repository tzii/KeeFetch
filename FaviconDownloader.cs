using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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
                { "Direct Site", () => new DirectSiteProvider() },
                { "Twenty Icons", () => new TwentyIconsProvider() },
                { "DuckDuckGo", () => new DuckDuckGoProvider() },
                { "Google", () => new GoogleProvider() },
                { "Yandex", () => new YandexProvider() },
                { "Favicone", () => new FaviconeProvider() },
                { "Icon Horse", () => new IconHorseProvider() }
            };

        private static readonly object certLock = new object();
        private static int certSetupCount;
        private static RemoteCertificateValidationCallback savedOriginalCallback;

        private static readonly ConcurrentDictionary<string, CachedIconEntry> DownloadCache =
            new ConcurrentDictionary<string, CachedIconEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProviderSemaphores =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private readonly Configuration config;
        private readonly IconSelector selector = new IconSelector();
        private readonly ConcurrentDictionary<string, ProviderHealthState> providerHealth =
            new ConcurrentDictionary<string, ProviderHealthState>(StringComparer.OrdinalIgnoreCase);

        private const int DefaultMaxCumulativeTimeoutMs = 45000;
        private const int DefaultPrimaryProviderTimeoutMs = 10000;
        private const int DefaultFallbackProviderTimeoutMs = 5000;

        public FaviconDownloader(Configuration config)
        {
            this.config = config;
        }

        /// <summary>Configures TLS 1.1/1.2/1.3 if available on this .NET version.</summary>
        public static void SetupTls()
        {
            try
            {
                var spt = SecurityProtocolType.Tls;
                Type tSpt = typeof(SecurityProtocolType);
                foreach (string name in Enum.GetNames(tSpt))
                {
                    if (name.Equals("Tls11", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Tls12", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Tls13", StringComparison.OrdinalIgnoreCase))
                    {
                        spt |= (SecurityProtocolType)Enum.Parse(tSpt, name, true);
                    }
                }

                ServicePointManager.SecurityProtocol = spt;
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
        /// Installs or removes a permissive certificate callback that accepts self-signed certs.
        /// </summary>
        public static void SetupSelfSignedCerts(bool allow)
        {
            lock (certLock)
            {
                if (allow)
                {
                    certSetupCount++;
                    if (certSetupCount == 1)
                    {
                        savedOriginalCallback = ServicePointManager.ServerCertificateValidationCallback;
                        ServicePointManager.ServerCertificateValidationCallback =
                            (object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) =>
                            {
                                if (errors == SslPolicyErrors.None)
                                    return true;

                                if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0 &&
                                    (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0)
                                    return true;

                                var original = savedOriginalCallback;
                                if (original != null)
                                    return original(sender, cert, chain, errors);
                                return false;
                            };
                    }
                }
                else
                {
                    certSetupCount--;
                    if (certSetupCount <= 0)
                    {
                        certSetupCount = 0;
                        ServicePointManager.ServerCertificateValidationCallback = savedOriginalCallback;
                        savedOriginalCallback = null;
                    }
                }
            }
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
        }

        public async Task<FaviconResult> DownloadAsync(string url, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();

            int timeoutMs = Math.Max(5000, config.Timeout * 1000);
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

            var request = new IconRequest
            {
                OriginalUrl = url,
                TargetHost = host,
                TargetOrigin = normalizedUri.GetLeftPart(UriPartial.Authority),
                CacheKey = cacheKey,
                MaxIconSize = maxSize,
                TimeoutMs = timeoutMs,
                AllowPrivateResponse = isPrivate
            };

            var collection = await CollectCandidatesAsync(request, isPrivate, token).ConfigureAwait(false);
            var selection = selector.Select(collection.Candidates, collection.AttemptedProviders,
                config.AllowSyntheticFallbacks);
            var result = BuildResultFromSelection(selection, host, cacheKey, maxSize);
            result.ProviderMetrics = collection.ProviderMetrics;
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            return result;
        }

        private async Task<FaviconResult> DownloadAndroidIconAsync(string url, int maxSize, int timeoutMs,
            CancellationToken token = default(CancellationToken))
        {
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
                        Util.IsPrivateHost(resolvedDomain), token).ConfigureAwait(false);
                    combinedCandidates.AddRange(collected.Candidates);
                    attemptedProviders.AddRange(collected.AttemptedProviders);
                    providerMetrics.AddRange(collected.ProviderMetrics);
                }
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                token.ThrowIfCancellationRequested();
                attemptedProviders.Add("Google Play");
                var playStopwatch = Stopwatch.StartNew();
                var playCandidate = await AndroidAppMapper.FetchGooglePlayIconCandidateAsync(
                    packageName, Math.Max(2000, Math.Min(7000, timeoutMs)), token).ConfigureAwait(false);
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

            if (string.IsNullOrWhiteSpace(cacheKey) && !string.IsNullOrWhiteSpace(packageName))
                cacheKey = "androidapp://" + packageName.ToLowerInvariant();

            var selection = selector.Select(combinedCandidates, attemptedProviders,
                config.AllowSyntheticFallbacks);
            var result = BuildResultFromSelection(selection, hostForResult, cacheKey, maxSize);
            result.ProviderMetrics = providerMetrics;
            return result;
        }

        private async Task<CandidateCollectionResult> CollectCandidatesAsync(IconRequest request,
            bool isPrivateTarget, CancellationToken token)
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

                int remaining = (int)Math.Max(0, GetMaxCumulativeTimeoutMs() - stopwatch.ElapsedMilliseconds);
                if (remaining < 1000)
                    break;

                int providerTimeout = GetProviderTimeout(provider, request.TimeoutMs, remaining);
                if (providerTimeout < 1000)
                    break;

                var providerRequest = CloneRequest(request, providerTimeout);

                IReadOnlyList<IconCandidate> candidates;
                var providerStopwatch = Stopwatch.StartNew();
                string providerOutcome = "empty";
                int candidateCount = 0;
                try
                {
                    candidates = await ExecuteProviderWithConcurrencyAsync(provider, providerRequest, token)
                        .ConfigureAwait(false);
                    candidateCount = candidates != null ? candidates.Count : 0;
                    providerOutcome = candidateCount > 0 ? "candidate" : "empty";
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

            return result;
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
            var orderedNames = config.GetProviderOrderList();
            var allNames = new List<string>();

            if (orderedNames != null)
                allNames.AddRange(orderedNames);

            foreach (var provider in DefaultProviderOrder)
                allNames.Add(provider);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedProviders = new List<IIconProvider>();

            foreach (string providerName in allNames)
            {
                if (string.IsNullOrWhiteSpace(providerName))
                    continue;
                if (!seen.Add(providerName))
                    continue;

                Func<IIconProvider> factory;
                if (!ProviderFactories.TryGetValue(providerName, out factory))
                    continue;

                if (!config.IsProviderEnabled(providerName))
                    continue;

                var provider = factory();
                if (provider.Capabilities.IsThirdParty && !config.UseThirdPartyFallbacks)
                    continue;

                if (isPrivateTarget && !provider.Capabilities.AllowPrivateHosts)
                    continue;

                orderedProviders.Add(provider);
            }

            var active = new List<IIconProvider>();
            var cooledDown = new List<IIconProvider>();
            foreach (var provider in orderedProviders)
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
            if (provider == null || candidates == null || candidates.Count == 0)
                return false;

            // Safe fast path: if Direct Site already produced a strong non-blank raster icon,
            // there is little value in querying every resolver just to confirm it.
            if (provider.Name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase) &&
                candidates.Any(c => IsStrongStoppingCandidate(c, provider.Name, IconTier.SiteCanonical, 0.90)))
            {
                return true;
            }

            if (!config.ShouldStopAfterStrongResolvedProvider())
                return false;

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

        private int GetMaxCumulativeTimeoutMs()
        {
            if (config == null)
                return DefaultMaxCumulativeTimeoutMs;

            if (config.FetchPresetMode == FetchPresetMode.Custom)
                return DefaultMaxCumulativeTimeoutMs;

            return Configuration.GetPresetMaxCumulativeTimeoutMs(config.FetchPresetMode);
        }

        private int GetProviderTimeout(IIconProvider provider, int requestedTimeoutMs, int remainingMs)
        {
            bool isPrimary = provider.Capabilities.DefaultTier == IconTier.SiteCanonical &&
                             provider.Name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase);

            int providerCap = isPrimary
                ? GetPrimaryProviderTimeoutMs()
                : GetFallbackProviderTimeoutMs();
            int timeout = Math.Min(requestedTimeoutMs, providerCap);
            timeout = Math.Min(timeout, remainingMs);
            return Math.Max(0, timeout);
        }

        private int GetPrimaryProviderTimeoutMs()
        {
            if (config == null)
                return DefaultPrimaryProviderTimeoutMs;

            if (config.FetchPresetMode == FetchPresetMode.Custom)
                return DefaultPrimaryProviderTimeoutMs;

            return Configuration.GetPresetPrimaryProviderTimeoutMs(config.FetchPresetMode);
        }

        private int GetFallbackProviderTimeoutMs()
        {
            if (config == null)
                return DefaultFallbackProviderTimeoutMs;

            if (config.FetchPresetMode == FetchPresetMode.Custom)
                return DefaultFallbackProviderTimeoutMs;

            return Configuration.GetPresetFallbackProviderTimeoutMs(config.FetchPresetMode);
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
        }

        internal sealed class CachedIconEntry
        {
            public byte[] IconData { get; set; }
            public string Provider { get; set; }
            public IconTier SelectedTier { get; set; }
            public bool WasSyntheticFallback { get; set; }
            public string DiagnosticsSummary { get; set; }
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
