using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace KeeFetch.IconProviders
{
    internal sealed class DirectSiteProvider : IIconProvider
    {
        public string Name => "Direct Site";

        private const int MaxCandidates = 8;
        private const long MaxIconBytes = 512 * 1024;
        private const long MaxHtmlBytes = 10 * 1024 * 1024;

        private static readonly string[] WellKnownPaths = new[]
        {
            "/favicon.ico",
            "/apple-touch-icon.png"
        };

        public byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            return GetIconWithOrigin("https://" + host, size, timeoutMs, proxy, false, token);
        }

        public byte[] GetIconWithOrigin(string origin, int size, int timeoutMs, IWebProxy proxy,
            bool allowPrivateResponse, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            string baseUrl = origin;
            var cookies = new CookieContainer();

            int probeTimeout = Math.Min(1500, timeoutMs);
            foreach (string path in WellKnownPaths)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    Uri responseUri;
                    byte[] data = DownloadData(baseUrl + path, probeTimeout, proxy,
                        cookies, MaxIconBytes, out responseUri, allowPrivateResponse, token);
                    if (data != null && Util.IsValidImage(data))
                        return data;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Debug("GetIconWithOrigin", ex); }
            }

            int htmlTimeout = Math.Min(3000, timeoutMs);
            Uri htmlResponseUri;
            byte[] htmlData = DownloadData(baseUrl, htmlTimeout, proxy,
                cookies, MaxHtmlBytes, out htmlResponseUri, allowPrivateResponse, token);

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
                    Uri iconResponseUri;
                    byte[] iconData = DownloadData(candidate.Url, candidateTimeout, proxy,
                        cookies, MaxIconBytes, out iconResponseUri, allowPrivateResponse, token);
                    if (iconData != null && Util.IsValidImage(iconData))
                        return iconData;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Debug("GetIconWithOrigin", ex); }
            }

            return null;
        }

        private List<IconCandidate> ParseIconLinks(string html, string baseUrl)
        {
            var results = new List<IconCandidate>();

            if (string.IsNullOrEmpty(html))
                return results;

            string head = html;
            var headMatch = Regex.Match(html, @"<head[^\u003e]*>(.*?)</head>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (headMatch.Success)
                head = headMatch.Groups[1].Value;

            head = Regex.Replace(head, @"<!--.*?-->", string.Empty,
                RegexOptions.Singleline);
            head = Regex.Replace(head, @"<script[^\u003e]*>.*?</script>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            head = Regex.Replace(head, @"<style[^\u003e]*>.*?</style>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            string resolvedBase = baseUrl;
            var baseMatch = Regex.Match(head, @"<base[^\u003e]+href\s*=\s*[""']([^""']+)[""']",
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
                @"<link\b[^\u003e]*\brel\s*=\s*[""']?(?:shortcut\s+)?icon[""']?[^\u003e]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var appleLinkPattern = new Regex(
                @"<link\b[^\u003e]*\brel\s*=\s*[""']?apple-touch-icon(?:-precomposed)?[""']?[^\u003e]*>",
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
                    iconSize = Math.Max(int.Parse(sizeMatch.Groups[1].Value),
                                       int.Parse(sizeMatch.Groups[2].Value));
                else
                    iconSize = 180;

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
                    iconSize = Math.Max(int.Parse(sizeMatch.Groups[1].Value),
                                       int.Parse(sizeMatch.Groups[2].Value));

                int priority = iconSize >= 32 ? 2 : 5;
                results.Add(new IconCandidate { Url = href, Size = iconSize, Priority = priority });
            }

            var ogImagePattern = new Regex(
                @"<meta\b[^\u003e]*\bproperty\s*=\s*[""']?og:image[""']?[^\u003e]*\bcontent\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);
            var ogMatch = ogImagePattern.Match(head);
            if (!ogMatch.Success)
            {
                ogImagePattern = new Regex(
                    @"<meta\b[^\u003e]*\bcontent\s*=\s*[""']?([^""'\s>]+)[^\u003e]*\bproperty\s*=\s*[""']?og:image[""']?",
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

        private byte[] DownloadData(string url, int timeoutMs, IWebProxy proxy,
            CookieContainer cookies, long maxBytes, out Uri responseUri,
            bool allowPrivateResponse = false, CancellationToken token = default(CancellationToken))
        {
            responseUri = null;
            try
            {
                token.ThrowIfCancellationRequested();

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs * 2;
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 10;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.9");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.CookieContainer = cookies;

                if (proxy != null)
                    request.Proxy = proxy;

                using (token.Register(() => request.Abort(), useSynchronizationContext: false))
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    responseUri = response.ResponseUri;

                    if (!allowPrivateResponse && responseUri != null)
                    {
                        string responseHost = responseUri.Host;
                        if (Util.IsPrivateHost(responseHost))
                            return null;
                    }

                    using (var stream = response.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        if (stream == null)
                            return null;

                        byte[] buffer = new byte[8192];
                        int read;
                        long total = 0;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            ms.Write(buffer, 0, read);
                            total += read;
                            if (total > maxBytes)
                                return null;
                        }

                        return ms.ToArray();
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Debug("DownloadData", ex);
                return null;
            }
        }

        private class IconCandidate
        {
            public string Url { get; set; }
            public int Size { get; set; }
            public int Priority { get; set; }
        }
    }
}
