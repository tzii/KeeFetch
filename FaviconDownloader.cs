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

        private static readonly ConcurrentDictionary<string, byte[]> DownloadCache =
            new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProviderSemaphores =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, ProviderHealthState> ProviderHealth =
            new ConcurrentDictionary<string, ProviderHealthState>(StringComparer.OrdinalIgnoreCase);

        private readonly Configuration config;
        private readonly IconSelector selector = new IconSelector();

        private const int MaxCumulativeTimeoutMs = 45000;
        private const int PrimaryProviderTimeoutMs = 10000;
        private const int FallbackProviderTimeoutMs = 5000;

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

            byte[] data;
            DownloadCache.TryGetValue(cacheKey, out data);
            return data;
        }

        public static void CacheIcon(string cacheKey, byte[] iconData)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || iconData == null)
                return;

            DownloadCache.TryAdd(cacheKey, iconData);
        }

        public static void ClearCache()
        {
            DownloadCache.Clear();
        }

        public async Task<FaviconResult> DownloadAsync(string url, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            int timeoutMs = Math.Max(5000, config.Timeout * 1000);
            int maxSize = config.MaxIconSize;

            if (AndroidAppMapper.IsAndroidUrl(url))
                return await DownloadAndroidIconAsync(url, maxSize, timeoutMs, token).ConfigureAwait(false);

            Uri normalizedUri;
            if (!Util.TryParseHttpUri(url, config.PrefixUrls, out normalizedUri))
            {
                return new FaviconResult
                {
                    Status = FaviconStatus.NotFound
                };
            }

            string host = normalizedUri.Host;
            string cacheKey = Util.GetNormalizedOriginKey(normalizedUri);
            bool isPrivate = Util.IsPrivateHost(host);

            byte[] cached = GetCachedIcon(cacheKey);
            if (cached != null)
            {
                return new FaviconResult
                {
                    IconData = cached,
                    Status = FaviconStatus.Success,
                    Provider = "Cache",
                    Host = host,
                    CacheKey = cacheKey,
                    SelectedTier = IconTier.SiteCanonical,
                    DiagnosticsSummary = "cache-hit"
                };
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

            return BuildResultFromSelection(selection, host, cacheKey, maxSize);
        }

        private async Task<FaviconResult> DownloadAndroidIconAsync(string url, int maxSize, int timeoutMs,
            CancellationToken token = default(CancellationToken))
        {
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
                    byte[] cached = GetCachedIcon(cacheKey);
                    if (cached != null)
                    {
                        return new FaviconResult
                        {
                            IconData = cached,
                            Status = FaviconStatus.Success,
                            Provider = "Cache",
                            Host = resolvedDomain,
                            CacheKey = cacheKey,
                            SelectedTier = IconTier.SiteCanonical,
                            DiagnosticsSummary = "cache-hit"
                        };
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
                }
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                token.ThrowIfCancellationRequested();
                attemptedProviders.Add("Google Play");
                var playCandidate = await AndroidAppMapper.FetchGooglePlayIconCandidateAsync(
                    packageName, Math.Max(2000, Math.Min(7000, timeoutMs)), token).ConfigureAwait(false);
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
            return BuildResultFromSelection(selection, hostForResult, cacheKey, maxSize);
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

                int remaining = (int)Math.Max(0, MaxCumulativeTimeoutMs - stopwatch.ElapsedMilliseconds);
                if (remaining < 1000)
                    break;

                int providerTimeout = GetProviderTimeout(provider, request.TimeoutMs, remaining);
                if (providerTimeout < 1000)
                    break;

                var providerRequest = CloneRequest(request, providerTimeout);

                IReadOnlyList<IconCandidate> candidates;
                try
                {
                    candidates = await ExecuteProviderWithConcurrencyAsync(provider, providerRequest, token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn("CollectCandidatesAsync", ex);
                    candidates = null;
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
                }
                else
                {
                    RecordProviderFailure(provider.Name);
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

        private static bool IsProviderInCooldown(string providerName)
        {
            ProviderHealthState state;
            if (!ProviderHealth.TryGetValue(providerName, out state))
                return false;

            return state.CooldownUntilUtc > DateTime.UtcNow;
        }

        private static void RecordProviderSuccess(string providerName)
        {
            ProviderHealth.AddOrUpdate(providerName,
                _ => new ProviderHealthState(0, DateTime.MinValue),
                (_, __) => new ProviderHealthState(0, DateTime.MinValue));
        }

        private static void RecordProviderFailure(string providerName)
        {
            ProviderHealth.AddOrUpdate(providerName,
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

        private static int GetProviderTimeout(IIconProvider provider, int requestedTimeoutMs, int remainingMs)
        {
            bool isPrimary = provider.Capabilities.DefaultTier == IconTier.SiteCanonical &&
                             provider.Name.Equals("Direct Site", StringComparison.OrdinalIgnoreCase);

            int providerCap = isPrimary ? PrimaryProviderTimeoutMs : FallbackProviderTimeoutMs;
            int timeout = Math.Min(requestedTimeoutMs, providerCap);
            timeout = Math.Min(timeout, remainingMs);
            return Math.Max(0, timeout);
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
                CacheIcon(cacheKey, resized);

            return result;
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
            }

            public List<IconCandidate> Candidates { get; private set; }
            public List<string> AttemptedProviders { get; private set; }
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
        public IconSelectionResult Selection { get; set; }
    }
}
