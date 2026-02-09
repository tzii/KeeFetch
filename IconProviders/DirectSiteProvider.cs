using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace KeeFetch.IconProviders
{
    internal sealed class DirectSiteProvider : IIconProvider
    {
        public string Name => "Direct Site";

        private static readonly string[] FaviconPaths = new[]
        {
            "/apple-touch-icon.png",
            "/apple-touch-icon-precomposed.png",
            "/favicon-96x96.png",
            "/favicon-32x32.png",
            "/favicon.ico"
        };

        public byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy)
        {
            string baseUrl = "https://" + host;

            var candidates = new List<IconCandidate>();

            byte[] htmlData = DownloadData(baseUrl, timeoutMs, proxy);
            if (htmlData != null)
            {
                string html = Encoding.UTF8.GetString(htmlData);
                candidates.AddRange(ParseIconLinks(html, baseUrl));
            }

            foreach (string path in FaviconPaths)
            {
                candidates.Add(new IconCandidate
                {
                    Url = baseUrl + path,
                    Size = 0,
                    Priority = 10
                });
            }

            candidates = candidates
                .GroupBy(c => c.Url.ToLowerInvariant())
                .Select(g => g.OrderByDescending(c => c.Size).ThenBy(c => c.Priority).First())
                .OrderBy(c => c.Priority)
                .ThenByDescending(c => c.Size)
                .ToList();

            foreach (var candidate in candidates)
            {
                try
                {
                    byte[] iconData = DownloadData(candidate.Url, timeoutMs, proxy);
                    if (iconData != null && Util.IsValidImage(iconData))
                        return iconData;
                }
                catch { }
            }

            byte[] httpData = DownloadData("http://" + host, timeoutMs, proxy);
            if (httpData != null)
            {
                string html = Encoding.UTF8.GetString(httpData);
                var httpCandidates = ParseIconLinks(html, "http://" + host);

                foreach (var candidate in httpCandidates.OrderBy(c => c.Priority).ThenByDescending(c => c.Size))
                {
                    try
                    {
                        byte[] iconData = DownloadData(candidate.Url, timeoutMs, proxy);
                        if (iconData != null && Util.IsValidImage(iconData))
                            return iconData;
                    }
                    catch { }
                }
            }

            return null;
        }

        private List<IconCandidate> ParseIconLinks(string html, string baseUrl)
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
                resolvedBase = baseMatch.Groups[1].Value.TrimEnd('/');

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
            catch
            {
                return null;
            }
        }

        private byte[] DownloadData(string url, int timeoutMs, IWebProxy proxy)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs * 2;
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 10;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.9");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.CookieContainer = new CookieContainer();

                if (proxy != null)
                    request.Proxy = proxy;

                using (var response = (HttpWebResponse)request.GetResponse())
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
                        ms.Write(buffer, 0, read);
                        total += read;
                        if (total > 10 * 1024 * 1024)
                            return null;
                    }

                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private class IconCandidate
        {
            public string Url;
            public int Size;
            public int Priority;
        }
    }
}
