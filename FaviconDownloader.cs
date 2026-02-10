using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using KeeFetch.IconProviders;

namespace KeeFetch
{
    /// <summary>
    /// Orchestrates favicon downloads with a provider chain: direct site → Google → DuckDuckGo → Icon Horse → Yandex.
    /// </summary>
    internal sealed class FaviconDownloader
    {
        private static readonly IIconProvider[] FallbackProviders = new IIconProvider[]
        {
            new GoogleProvider(),
            new DuckDuckGoProvider(),
            new IconHorseProvider(),
            new YandexProvider()
        };

        private static readonly object certLock = new object();
        private static int certSetupCount = 0;
        private static RemoteCertificateValidationCallback savedOriginalCallback;

        private readonly Configuration config;
        private readonly IWebProxy proxy;

        // Cumulative timeout for all attempts on a single entry (prevents hang on many fallbacks)
        private const int MaxCumulativeTimeoutMs = 45000; // 45 seconds max per entry
        // Reduced timeout for fallback providers (faster failure)
        private const int FallbackTimeoutMs = 5000; // 5 seconds per fallback provider

        public FaviconDownloader(Configuration config, IWebProxy proxy)
        {
            this.config = config;
            this.proxy = proxy;
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
            }
            catch (Exception ex) { Logger.Warn("SetupTls", ex); }
        }

        /// <summary>
        /// Installs or removes a permissive certificate callback that accepts self-signed certs.
        /// Thread-safe: uses a reference count so concurrent FaviconDialog runs don't stomp each other.
        /// Limitation: <see cref="ServicePointManager.ServerCertificateValidationCallback"/> is
        /// process-global in .NET Framework, so other plugins may also be affected.
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
                        // First caller — snapshot whatever callback is currently installed
                        savedOriginalCallback = ServicePointManager.ServerCertificateValidationCallback;
                        ServicePointManager.ServerCertificateValidationCallback =
                            (object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) =>
                            {
                                if (errors == SslPolicyErrors.None)
                                    return true;
                                // Accept chain errors (self-signed) but reject name mismatches
                                if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0 &&
                                    (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0)
                                    return true;
                                // Delegate to the original callback if one existed
                                var orig = savedOriginalCallback;
                                if (orig != null)
                                    return orig(sender, cert, chain, errors);
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

        /// <summary>
        /// Attempts to download a favicon for <paramref name="url"/>.
        /// Respects <paramref name="token"/> for cancellation.
        /// </summary>
        public FaviconResult Download(string url, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            var result = new FaviconResult();
            int primaryTimeoutMs = config.Timeout * 1000;
            int maxSize = config.MaxIconSize;

            // Start cumulative timer
            var stopwatch = Stopwatch.StartNew();

            if (AndroidAppMapper.IsAndroidUrl(url))
            {
                return DownloadAndroidIcon(url, primaryTimeoutMs, maxSize, stopwatch, token);
            }

            string host = Util.ExtractHost(url);
            if (string.IsNullOrEmpty(host))
            {
                if (config.PrefixUrls)
                {
                    host = Util.ExtractHost("https://" + url);
                }
                if (string.IsNullOrEmpty(host))
                {
                    result.Status = FaviconStatus.NotFound;
                    return result;
                }
            }

            result.Host = host;
            bool isPrivate = Util.IsPrivateHost(host);

            string hostWithPort = Util.ExtractHostWithPort(url);
            if (string.IsNullOrEmpty(hostWithPort))
                hostWithPort = host;

            string explicitScheme = Util.ExtractScheme(url);

            // Primary attempt — capped so fallback providers always get a chance
            int directTimeoutMs = Math.Min(primaryTimeoutMs, 10000);
            int directRemainingMs = (int)Math.Max(0, MaxCumulativeTimeoutMs - stopwatch.ElapsedMilliseconds);
            directTimeoutMs = Math.Min(directTimeoutMs, directRemainingMs);

            var directProvider = new DirectSiteProvider();
            byte[] iconData = null;
            if (directTimeoutMs >= 1000)
            {
                if (isPrivate)
                {
                    iconData = TryDirectPrivate(directProvider, hostWithPort, explicitScheme,
                        maxSize, directTimeoutMs, isPrivate, token);
                }
                else
                {
                    string origin = (explicitScheme ?? "https") + "://" + hostWithPort;
                    iconData = directProvider.GetIconWithOrigin(origin, maxSize, directTimeoutMs, proxy, false, token);
                }
            }

            if (iconData != null)
            {
                byte[] resized = Util.ResizeImage(iconData, maxSize, maxSize);
                if (resized != null)
                {
                    result.IconData = resized;
                    result.Status = FaviconStatus.Success;
                    result.Provider = directProvider.Name;
                    return result;
                }
            }

            // Fallback attempts with reduced timeout and cumulative limit
            // Skip third-party fallbacks for private hosts (they can't resolve them)
            if (!config.UseThirdPartyFallbacks || isPrivate)
            {
                result.Status = FaviconStatus.NotFound;
                return result;
            }

            foreach (var provider in FallbackProviders)
            {
                token.ThrowIfCancellationRequested();

                // Check cumulative timeout
                if (stopwatch.ElapsedMilliseconds >= MaxCumulativeTimeoutMs)
                    break;

                int remainingMs = (int)Math.Max(0, MaxCumulativeTimeoutMs - stopwatch.ElapsedMilliseconds);
                int effectiveTimeout = Math.Min(FallbackTimeoutMs, remainingMs);

                if (effectiveTimeout < 1000) // Less than 1 second remaining, not worth trying
                    break;

                try
                {
                    iconData = provider.GetIcon(host, maxSize, effectiveTimeout, proxy, token);
                    if (iconData != null)
                    {
                        byte[] resized = Util.ResizeImage(iconData, maxSize, maxSize);
                        if (resized != null)
                        {
                            result.IconData = resized;
                            result.Status = FaviconStatus.Success;
                            result.Provider = provider.Name;
                            return result;
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Warn("Download", ex); }
            }

            result.Status = FaviconStatus.NotFound;
            return result;
        }

        private byte[] TryDirectPrivate(DirectSiteProvider provider, string hostWithPort,
            string explicitScheme, int maxSize, int timeoutMs, bool isPrivate,
            CancellationToken token = default(CancellationToken))
        {
            string[] schemes;
            if (explicitScheme != null)
                schemes = new[] { explicitScheme };
            else
                schemes = new[] { "http", "https" };

            int perSchemeTimeout = schemes.Length > 1 ? timeoutMs / 2 : timeoutMs;
            perSchemeTimeout = Math.Max(perSchemeTimeout, 1000);

            foreach (string scheme in schemes)
            {
                token.ThrowIfCancellationRequested();
                string origin = scheme + "://" + hostWithPort;
                byte[] data = provider.GetIconWithOrigin(origin, maxSize, perSchemeTimeout, proxy, true, token);
                if (data != null)
                    return data;
            }

            return null;
        }

        private FaviconResult DownloadAndroidIcon(string url, int primaryTimeoutMs, int maxSize, Stopwatch stopwatch,
            CancellationToken token = default(CancellationToken))
        {
            var result = new FaviconResult();
            string packageName = AndroidAppMapper.GetPackageName(url);
            string domain = AndroidAppMapper.MapToWebDomain(url);

            // Helper to check if we've exceeded cumulative timeout
            bool IsTimeUp() => stopwatch.ElapsedMilliseconds >= MaxCumulativeTimeoutMs;
            int GetRemainingMs() => (int)Math.Max(0, MaxCumulativeTimeoutMs - stopwatch.ElapsedMilliseconds);
            int GetEffectiveTimeout(bool isPrimary) 
            {
                int remaining = GetRemainingMs();
                int baseTimeout = isPrimary ? primaryTimeoutMs : FallbackTimeoutMs;
                return Math.Min(baseTimeout, remaining);
            }

            if (!string.IsNullOrEmpty(domain))
            {
                result.Host = domain;

                // Try direct site provider with primary timeout
                if (!IsTimeUp())
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var directProvider = new DirectSiteProvider();
                        int timeout = Math.Min(GetEffectiveTimeout(true), 10000);
                        if (timeout >= 1000)
                        {
                            byte[] iconData = directProvider.GetIcon(domain, maxSize, timeout, proxy, token);
                            if (iconData != null)
                            {
                                byte[] resized = Util.ResizeImage(iconData, maxSize, maxSize);
                                if (resized != null)
                                {
                                    result.IconData = resized;
                                    result.Status = FaviconStatus.Success;
                                    result.Provider = directProvider.Name;
                                    return result;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { Logger.Debug("DownloadAndroidIcon", ex); }
                }

                // Try fallback providers with reduced timeout
                if (config.UseThirdPartyFallbacks && !Util.IsPrivateHost(domain))
                {
                    foreach (var provider in FallbackProviders)
                    {
                        if (IsTimeUp()) break;

                        token.ThrowIfCancellationRequested();

                        int timeout = GetEffectiveTimeout(false);
                        if (timeout < 1000) break;

                        try
                        {
                            byte[] iconData = provider.GetIcon(domain, maxSize, timeout, proxy, token);
                            if (iconData != null)
                            {
                                byte[] resized = Util.ResizeImage(iconData, maxSize, maxSize);
                                if (resized != null)
                                {
                                    result.IconData = resized;
                                    result.Status = FaviconStatus.Success;
                                    result.Provider = provider.Name;
                                    return result;
                                }
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { Logger.Warn("DownloadAndroidIcon", ex); }
                    }
                }
            }

            // Try Google Play Store icon
            if (!string.IsNullOrEmpty(packageName) && !IsTimeUp())
            {
                if (string.IsNullOrEmpty(result.Host))
                    result.Host = packageName;

                int timeout = GetEffectiveTimeout(false);
                if (timeout >= 2000) // Google Play needs a bit more time
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        byte[] playIcon = AndroidAppMapper.FetchGooglePlayIcon(packageName, timeout, proxy, token);
                        if (playIcon != null)
                        {
                            byte[] resized = Util.ResizeImage(playIcon, maxSize, maxSize);
                            if (resized != null)
                            {
                                result.IconData = resized;
                                result.Status = FaviconStatus.Success;
                                result.Provider = "Google Play";
                                return result;
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { Logger.Warn("DownloadAndroidIcon", ex); }
                }

                // Try guessed domain from package name
                if (!IsTimeUp())
                {
                    string guessedDomain = AndroidAppMapper.TryGuessFromPackage(packageName);
                    if (!string.IsNullOrEmpty(guessedDomain) &&
                        !guessedDomain.Equals(domain, StringComparison.OrdinalIgnoreCase) &&
                        config.UseThirdPartyFallbacks &&
                        !Util.IsPrivateHost(guessedDomain))
                    {
                        foreach (var provider in FallbackProviders)
                        {
                            if (IsTimeUp()) break;

                            token.ThrowIfCancellationRequested();

                            timeout = GetEffectiveTimeout(false);
                            if (timeout < 1000) break;

                            try
                            {
                                byte[] iconData = provider.GetIcon(guessedDomain, maxSize, timeout, proxy, token);
                                if (iconData != null)
                                {
                                    byte[] resized = Util.ResizeImage(iconData, maxSize, maxSize);
                                    if (resized != null)
                                    {
                                        result.IconData = resized;
                                        result.Status = FaviconStatus.Success;
                                        result.Provider = provider.Name;
                                        result.Host = guessedDomain;
                                        return result;
                                    }
                                }
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception ex) { Logger.Warn("DownloadAndroidIcon", ex); }
                        }
                    }
                }
            }

            result.Status = FaviconStatus.NotFound;
            return result;
        }
    }

    /// <summary>Result status of a favicon download attempt.</summary>
    internal enum FaviconStatus
    {
        Success,
        NotFound
    }

    /// <summary>Result of a favicon download attempt.</summary>
    internal sealed class FaviconResult
    {
        public byte[] IconData { get; set; }
        public FaviconStatus Status { get; set; } = FaviconStatus.NotFound;
        public string Provider { get; set; }
        public string Host { get; set; }
    }
}
