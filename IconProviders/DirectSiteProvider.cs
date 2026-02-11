using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    internal sealed class DirectSiteProvider : IIconProvider
    {
        public string Name => "Direct Site";

        private const int MaxCandidates = 8;
        /// <summary>Maximum icon download size (512 KB).</summary>
        private const long MaxIconBytes = 512 * 1024;
        /// <summary>Maximum HTML download size for icon link parsing (256 KB — icon links are in &lt;head&gt;).</summary>
        private const long MaxHtmlBytes = 256 * 1024;

        private static readonly string[] WellKnownPaths = new[]
        {
            "/favicon.ico",
            "/apple-touch-icon.png"
        };

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler);
        }

        public Task<byte[]> GetIconAsync(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            return GetIconWithOriginAsync("https://" + host, size, timeoutMs, proxy, false, token);
        }

        public async Task<byte[]> GetIconWithOriginAsync(string origin, int size, int timeoutMs, IWebProxy proxy,
            bool allowPrivateResponse, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            string baseUrl = origin;

            int probeTimeout = Math.Min(1500, timeoutMs);
            foreach (string path in WellKnownPaths)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    var (data, responseUri) = await DownloadDataAsync(baseUrl + path, probeTimeout, proxy,
                        MaxIconBytes, allowPrivateResponse, token).ConfigureAwait(false);
                    if (data != null && Util.IsValidImage(data))
                        return data;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Debug("GetIconWithOrigin", ex); }
            }

            int htmlTimeout = Math.Min(3000, timeoutMs);
            var (htmlData, htmlResponseUri) = await DownloadDataAsync(baseUrl, htmlTimeout, proxy,
                MaxHtmlBytes, allowPrivateResponse, token).ConfigureAwait(false);

            if (htmlData == null)
                return null;

            string resolvedBase = htmlResponseUri != null
                ? htmlResponseUri.GetLeftPart(UriPartial.Authority)
                : baseUrl;

            Encoding encoding = Encoding.UTF8;
            string htmlStr = encoding.GetString(htmlData);
            var charsetMatch = Regex.Match(htmlStr.Substring(0, Math.Min(htmlStr.Length, 4096)),
                @"charset\s*=\s*[""']?([^""'\s;>]+)", RegexOptions.IgnoreCase);
            if (charsetMatch.Success)
            {
                try
                {
                    encoding = Encoding.GetEncoding(charsetMatch.Groups[1].Value.Trim());
                    htmlStr = encoding.GetString(htmlData);
                }
                catch (Exception ex) { Logger.Debug("GetIconWithOrigin", ex); }
            }
            string html = htmlStr;
            var candidates = ParseIconLinks(html, resolvedBase);

            candidates = candidates
                .GroupBy(c => c.Url.ToLowerInvariant())
                .Select(g => g.OrderByDescending(c => c.Size).ThenBy(c => c.Priority).First())
                .OrderBy(c => c.Priority)
                .ThenByDescending(c => c.Size)
                .Take(MaxCandidates)
                .ToList();

            int candidateTimeout = Math.Min(2000, timeoutMs);
            foreach (var candidate in candidates)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    var (iconData, _) = await DownloadDataAsync(candidate.Url, candidateTimeout, proxy,
                        MaxIconBytes, allowPrivateResponse, token).ConfigureAwait(false);
                    if (iconData != null && Util.IsValidImage(iconData))
                        return iconData;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Debug("GetIconWithOrigin", ex); }
            }

            return null;
        }

        internal List<IconCandidate> ParseIconLinks(string html, string baseUrl)
        {
            var results = new List<IconCandidate>();

            if (string.IsNullOrEmpty(html))
                return results;

            string head = html;
            var headMatch = Regex.Match(html, @"<head[^>]*>(.*?)</head>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (headMatch.Success)
                head = headMatch.Groups[1].Value;

            head = Regex.Replace(head, @"<!--.*?-->", string.Empty,
                RegexOptions.Singleline);
            head = Regex.Replace(head, @"<script[^>]*>.*?</script>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            head = Regex.Replace(head, @"<style[^>]*>.*?</style>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            string resolvedBase = baseUrl;
            var baseMatch = Regex.Match(head, @"<base[^>]+href\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (baseMatch.Success)
            {
                string candidate = baseMatch.Groups[1].Value.TrimEnd('/');
                try
                {
                    var baseUri = new Uri(baseUrl);
                    var candidateUri = new Uri(candidate);
                    if (candidateUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
                        resolvedBase = candidate;
                }
                catch (Exception ex) { Logger.Debug("ParseIconLinks", ex); }
            }

            var linkPattern = new Regex(
                @"<link\b[^>]*\brel\s*=\s*[""']?(?:shortcut\s+)?icon[""']?[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var appleLinkPattern = new Regex(
                @"<link\b[^>]*\brel\s*=\s*[""']?apple-touch-icon(?:-precomposed)?[""']?[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var hrefPattern = new Regex(
                @"\bhref\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);

            var sizesPattern = new Regex(
                @"\bsizes\s*=\s*[""']?(\d+)x(\d+)",
                RegexOptions.IgnoreCase);

            foreach (Match linkMatch in appleLinkPattern.Matches(head))
            {
                var hrefMatch = hrefPattern.Match(linkMatch.Value);
                if (!hrefMatch.Success) continue;

                string href = ResolveUrl(hrefMatch.Groups[1].Value, resolvedBase);
                if (href == null) continue;

                int iconSize = 0;
                var sizeMatch = sizesPattern.Match(linkMatch.Value);
                if (sizeMatch.Success)
                {
                    // Use TryParse with overflow protection
                    if (int.TryParse(sizeMatch.Groups[1].Value, out int w) &&
                        int.TryParse(sizeMatch.Groups[2].Value, out int h))
                    {
                        iconSize = Math.Max(w, h);
                    }
                }
                else
                {
                    iconSize = 180;
                }

                results.Add(new IconCandidate { Url = href, Size = iconSize, Priority = 1 });
            }

            foreach (Match linkMatch in linkPattern.Matches(head))
            {
                var hrefMatch = hrefPattern.Match(linkMatch.Value);
                if (!hrefMatch.Success) continue;

                string href = ResolveUrl(hrefMatch.Groups[1].Value, resolvedBase);
                if (href == null) continue;

                int iconSize = 0;
                var sizeMatch = sizesPattern.Match(linkMatch.Value);
                if (sizeMatch.Success)
                {
                    // Use TryParse with overflow protection
                    if (int.TryParse(sizeMatch.Groups[1].Value, out int w) &&
                        int.TryParse(sizeMatch.Groups[2].Value, out int h))
                    {
                        iconSize = Math.Max(w, h);
                    }
                }

                int priority = iconSize >= 32 ? 2 : 5;
                results.Add(new IconCandidate { Url = href, Size = iconSize, Priority = priority });
            }

            var ogImagePattern = new Regex(
                @"<meta\b[^>]*\bproperty\s*=\s*[""']?og:image[""']?[^>]*\bcontent\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);
            var ogMatch = ogImagePattern.Match(head);
            if (!ogMatch.Success)
            {
                ogImagePattern = new Regex(
                    @"<meta\b[^>]*\bcontent\s*=\s*[""']?([^""'\s>]+)[^>]*\bproperty\s*=\s*[""']?og:image[""']?",
                    RegexOptions.IgnoreCase);
                ogMatch = ogImagePattern.Match(head);
            }
            if (ogMatch.Success)
            {
                string ogUrl = ResolveUrl(ogMatch.Groups[1].Value, resolvedBase);
                if (ogUrl != null)
                    results.Add(new IconCandidate { Url = ogUrl, Size = 200, Priority = 8 });
            }

            return results;
        }

        private string ResolveUrl(string href, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(href))
                return null;

            href = href.Trim();

            if (href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return null;

            if (href.StartsWith("//"))
                href = "https:" + href;

            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return href;

            try
            {
                var bUri = new Uri(baseUrl.TrimEnd('/') + "/");
                var resolved = new Uri(bUri, href);
                return resolved.AbsoluteUri;
            }
            catch (Exception ex)
            {
                Logger.Debug("ResolveUrl", ex);
                return null;
            }
        }

        private async Task<(byte[] Data, Uri ResponseUri)> DownloadDataAsync(string url, int timeoutMs, IWebProxy proxy,
            long maxBytes, bool allowPrivateResponse = false, CancellationToken token = default(CancellationToken))
        {
            Uri responseUri = null;
            try
            {
                token.ThrowIfCancellationRequested();

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(timeoutMs);
                        var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                        
                        if (!response.IsSuccessStatusCode)
                            return (null, null);

                        responseUri = response.RequestMessage.RequestUri;

                        if (!allowPrivateResponse && responseUri != null)
                        {
                            string responseHost = responseUri.Host;
                            if (Util.IsPrivateHost(responseHost))
                                return (null, null);
                        }

                        var contentLength = response.Content.Headers.ContentLength;
                        if (contentLength.HasValue && contentLength.Value > maxBytes)
                            return (null, null);

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var ms = new MemoryStream())
                        {
                            if (stream == null)
                                return (null, responseUri);

                            byte[] buffer = new byte[8192];
                            int read;
                            long total = 0;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                            {
                                await ms.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                total += read;
                                if (total > maxBytes)
                                    return (null, responseUri);
                            }

                            return (ms.ToArray(), responseUri);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Debug("DownloadDataAsync", ex);
                return (null, responseUri);
            }
        }

        internal class IconCandidate
        {
            public string Url { get; set; }
            public int Size { get; set; }
            public int Priority { get; set; }
        }
    }
}
