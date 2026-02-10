using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using KeeFetch.IconProviders;

namespace KeeFetch
{
    internal sealed class FaviconDownloader
    {
        private static readonly IIconProvider[] FallbackProviders = new IIconProvider[]
        {
            new GoogleProvider(),
            new DuckDuckGoProvider(),
            new IconHorseProvider(),
            new YandexProvider()
        };

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
            catch { }
        }

        public static RemoteCertificateValidationCallback SetupSelfSignedCerts(
            bool allow, RemoteCertificateValidationCallback originalCallback)
        {
            if (!allow)
            {
                ServicePointManager.ServerCertificateValidationCallback = originalCallback;
                return originalCallback;
            }

            ServicePointManager.ServerCertificateValidationCallback =
                (object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) =>
                {
                    if (errors == SslPolicyErrors.None)
                        return true;
                    if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0 &&
                        (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0)
                        return true;
                    if (originalCallback != null)
                        return originalCallback(sender, cert, chain, errors);
                    return false;
                };

            return ServicePointManager.ServerCertificateValidationCallback;
        }

        public FaviconResult Download(string url)
        {
            var result = new FaviconResult();
            int primaryTimeoutMs = config.Timeout * 1000;
            int maxSize = config.MaxIconSize;

            // Start cumulative timer
            var stopwatch = Stopwatch.StartNew();

            if (AndroidAppMapper.IsAndroidUrl(url))
            {
                return DownloadAndroidIcon(url, primaryTimeoutMs, maxSize, stopwatch);
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
                        maxSize, directTimeoutMs, isPrivate);
                }
                else
                {
                    string origin = (explicitScheme ?? "https") + "://" + hostWithPort;
                    iconData = directProvider.GetIconWithOrigin(origin, maxSize, directTimeoutMs, proxy, false);
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
                // Check cumulative timeout
                if (stopwatch.ElapsedMilliseconds >= MaxCumulativeTimeoutMs)
                    break;

                int remainingMs = (int)Math.Max(0, MaxCumulativeTimeoutMs - stopwatch.ElapsedMilliseconds);
                int effectiveTimeout = Math.Min(FallbackTimeoutMs, remainingMs);

                if (effectiveTimeout < 1000) // Less than 1 second remaining, not worth trying
                    break;

                try
                {
                    iconData = provider.GetIcon(host, maxSize, effectiveTimeout, proxy);
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
                catch { }
            }

            result.Status = FaviconStatus.NotFound;
            return result;
        }

        private byte[] TryDirectPrivate(DirectSiteProvider provider, string hostWithPort,
            string explicitScheme, int maxSize, int timeoutMs, bool isPrivate)
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
                string origin = scheme + "://" + hostWithPort;
                byte[] data = provider.GetIconWithOrigin(origin, maxSize, perSchemeTimeout, proxy, true);
                if (data != null)
                    return data;
            }

            return null;
        }

        private FaviconResult DownloadAndroidIcon(string url, int primaryTimeoutMs, int maxSize, Stopwatch stopwatch)
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
                        var directProvider = new DirectSiteProvider();
                        int timeout = Math.Min(GetEffectiveTimeout(true), 10000);
                        if (timeout >= 1000)
                        {
                            byte[] iconData = directProvider.GetIcon(domain, maxSize, timeout, proxy);
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
                    catch { }
                }

                // Try fallback providers with reduced timeout
                if (config.UseThirdPartyFallbacks && !Util.IsPrivateHost(domain))
                {
                    foreach (var provider in FallbackProviders)
                    {
                        if (IsTimeUp()) break;

                        int timeout = GetEffectiveTimeout(false);
                        if (timeout < 1000) break;

                        try
                        {
                            byte[] iconData = provider.GetIcon(domain, maxSize, timeout, proxy);
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
                        catch { }
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
                        byte[] playIcon = AndroidAppMapper.FetchGooglePlayIcon(packageName, timeout, proxy);
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
                    catch { }
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

                            timeout = GetEffectiveTimeout(false);
                            if (timeout < 1000) break;

                            try
                            {
                                byte[] iconData = provider.GetIcon(guessedDomain, maxSize, timeout, proxy);
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
                            catch { }
                        }
                    }
                }
            }

            result.Status = FaviconStatus.NotFound;
            return result;
        }
    }

    internal enum FaviconStatus
    {
        Success,
        NotFound,
        Error
    }

    internal sealed class FaviconResult
    {
        public byte[] IconData;
        public FaviconStatus Status = FaviconStatus.Error;
        public string Provider;
        public string Host;
    }
}
