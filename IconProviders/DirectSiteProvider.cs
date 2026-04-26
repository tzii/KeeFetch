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
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    internal sealed class DirectSiteProvider : IIconProvider
    {
        private const int MaxCandidates = 12;
        private const int MaxCandidateFetchConcurrency = 3;
        private const long MaxIconBytes = 512 * 1024;
        private const long MaxHtmlBytes = 256 * 1024;
        private const long MaxManifestBytes = 128 * 1024;

        private static readonly string[] WellKnownPaths = new[]
        {
            "/favicon.ico",
            "/apple-touch-icon.png",
            "/apple-touch-icon-precomposed.png"
        };

        private static readonly ProviderCapabilities capabilities =
            new ProviderCapabilities("Direct Site", IconTier.SiteCanonical,
                isThirdParty: false, isSyntheticCapable: false, isPlaceholderProne: false,
                concurrencyCap: 4, baseConfidence: 0.86, allowPrivateHosts: true);

        private static readonly Random RetryJitter = new Random();
        private static readonly object RetryJitterLock = new object();

        public string Name { get { return "Direct Site"; } }
        public ProviderCapabilities Capabilities { get { return capabilities; } }

        public async Task<IReadOnlyList<IconCandidate>> GetCandidatesAsync(IconRequest request,
            CancellationToken token = default(CancellationToken))
        {
            var results = new List<IconCandidate>();
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return results;

            token.ThrowIfCancellationRequested();

            bool allowPrivateResponse = request.AllowPrivateResponse;
            string origin = !string.IsNullOrWhiteSpace(request.TargetOrigin)
                ? request.TargetOrigin
                : "https://" + request.TargetHost;

            var discoveredLinks = new List<DiscoveredIconLink>();
            discoveredLinks.AddRange(BuildWellKnownCandidates(origin));

            int htmlTimeout = Math.Min(3000, Math.Max(1000, request.TimeoutMs));
            var htmlResult = await DownloadDataAsync(origin, htmlTimeout, MaxHtmlBytes,
                allowPrivateResponse, token).ConfigureAwait(false);

            if (htmlResult.Data != null)
            {
                string html = DecodeText(htmlResult.Data);
                string resolvedBase = htmlResult.ResponseUri != null
                    ? htmlResult.ResponseUri.AbsoluteUri
                    : origin;

                discoveredLinks.AddRange(ParseIconLinks(html, resolvedBase));

                var manifestLinks = ParseManifestLinks(html, resolvedBase);
                foreach (string manifestUrl in manifestLinks.Take(2))
                {
                    token.ThrowIfCancellationRequested();
                    int manifestTimeout = Math.Min(2500, Math.Max(1000, request.TimeoutMs));
                    var manifestResult = await DownloadDataAsync(manifestUrl, manifestTimeout,
                        MaxManifestBytes, allowPrivateResponse, token).ConfigureAwait(false);
                    if (manifestResult.Data == null)
                        continue;

                    string manifestJson = DecodeText(manifestResult.Data);
                    discoveredLinks.AddRange(ParseManifestIcons(manifestJson, manifestUrl));
                }
            }

            discoveredLinks = PrepareCandidateLinks(discoveredLinks);

            int candidateTimeout = Math.Min(2200, Math.Max(1000, request.TimeoutMs));
            results.AddRange(await DownloadCandidatesAsync(request.TargetHost, discoveredLinks,
                candidateTimeout, allowPrivateResponse, token).ConfigureAwait(false));

            return results;
        }

        internal List<DiscoveredIconLink> PrepareCandidateLinks(IEnumerable<DiscoveredIconLink> links)
        {
            if (links == null)
                return new List<DiscoveredIconLink>();

            return links
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Url))
                .GroupBy(c => GetCanonicalCandidateUrl(c.Url), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(c => c.Priority)
                    .ThenByDescending(c => c.Size)
                    .First())
                .OrderBy(c => c.Priority)
                .ThenByDescending(c => c.Size)
                .Take(MaxCandidates)
                .ToList();
        }

        private async Task<List<IconCandidate>> DownloadCandidatesAsync(string targetHost,
            List<DiscoveredIconLink> discoveredLinks, int candidateTimeout, bool allowPrivateResponse,
            CancellationToken token)
        {
            var results = new List<IconCandidate>();
            if (discoveredLinks == null || discoveredLinks.Count == 0)
                return results;

            int maxConcurrency = Math.Min(MaxCandidateFetchConcurrency, Math.Max(1, discoveredLinks.Count));
            using (var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency))
            {
                var tasks = new List<Task<CandidateDownloadOutcome>>();

                for (int i = 0; i < discoveredLinks.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(token).ConfigureAwait(false);

                    int order = i;
                    var link = discoveredLinks[i];
                    tasks.Add(DownloadCandidateWithSemaphoreAsync(targetHost, link, order,
                        candidateTimeout, allowPrivateResponse, semaphore, token));
                }

                CandidateDownloadOutcome[] outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var outcome in outcomes.OrderBy(o => o.Order))
                {
                    if (outcome.Candidate != null)
                        results.Add(outcome.Candidate);
                }
            }

            return results;
        }

        private async Task<CandidateDownloadOutcome> DownloadCandidateWithSemaphoreAsync(
            string targetHost, DiscoveredIconLink link, int order, int candidateTimeout,
            bool allowPrivateResponse, SemaphoreSlim semaphore, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var candidateResult = await DownloadDataAsync(link.Url, candidateTimeout,
                    MaxIconBytes, allowPrivateResponse, token).ConfigureAwait(false);
                if (candidateResult.Data == null)
                    return new CandidateDownloadOutcome(order, null);

                var candidate = BuildCandidate(targetHost, link, candidateResult);
                return new CandidateDownloadOutcome(order, candidate);
            }
            finally
            {
                semaphore.Release();
            }
        }

        internal List<DiscoveredIconLink> ParseIconLinks(string html, string baseUrl)
        {
            var results = new List<DiscoveredIconLink>();
            if (string.IsNullOrEmpty(html))
                return results;

            string head = ExtractHead(html);
            string resolvedBase = ResolveBaseTag(head, baseUrl);

            var iconPattern = new Regex(
                @"<link\b[^>]*\brel\s*=\s*[""']?(?:shortcut\s+)?icon[""']?[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var applePattern = new Regex(
                @"<link\b[^>]*\brel\s*=\s*[""']?apple-touch-icon(?:-precomposed)?[""']?[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var hrefPattern = new Regex(@"\bhref\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);
            var sizePattern = new Regex(@"\bsizes\s*=\s*[""']?(\d+)x(\d+)",
                RegexOptions.IgnoreCase);

            foreach (Match linkMatch in applePattern.Matches(head))
            {
                var hrefMatch = hrefPattern.Match(linkMatch.Value);
                if (!hrefMatch.Success) continue;

                string href = ResolveUrl(hrefMatch.Groups[1].Value, resolvedBase);
                if (href == null) continue;

                int iconSize = 180;
                var sizeMatch = sizePattern.Match(linkMatch.Value);
                if (sizeMatch.Success)
                {
                    int w, h;
                    if (int.TryParse(sizeMatch.Groups[1].Value, out w) &&
                        int.TryParse(sizeMatch.Groups[2].Value, out h))
                        iconSize = Math.Max(w, h);
                }

                results.Add(new DiscoveredIconLink
                {
                    Url = href,
                    Size = iconSize,
                    Priority = 1,
                    Tier = IconTier.SiteCanonical,
                    SourceType = "apple-touch-icon",
                    IsSvgHint = href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
                    BaseConfidence = 0.94
                });
            }

            foreach (Match linkMatch in iconPattern.Matches(head))
            {
                var hrefMatch = hrefPattern.Match(linkMatch.Value);
                if (!hrefMatch.Success) continue;

                string href = ResolveUrl(hrefMatch.Groups[1].Value, resolvedBase);
                if (href == null) continue;

                int iconSize = 0;
                var sizeMatch = sizePattern.Match(linkMatch.Value);
                if (sizeMatch.Success)
                {
                    int w, h;
                    if (int.TryParse(sizeMatch.Groups[1].Value, out w) &&
                        int.TryParse(sizeMatch.Groups[2].Value, out h))
                        iconSize = Math.Max(w, h);
                }

                int priority = iconSize >= 64 ? 2 : 4;
                results.Add(new DiscoveredIconLink
                {
                    Url = href,
                    Size = iconSize,
                    Priority = priority,
                    Tier = IconTier.SiteCanonical,
                    SourceType = "rel-icon",
                    IsSvgHint = href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
                    BaseConfidence = iconSize >= 64 ? 0.90 : 0.82
                });
            }

            string ogUrl = ParseOgImage(head, resolvedBase);
            if (ogUrl != null)
            {
                results.Add(new DiscoveredIconLink
                {
                    Url = ogUrl,
                    Size = 200,
                    Priority = 10,
                    Tier = IconTier.StrongResolved,
                    SourceType = "og:image-backup",
                    IsSvgHint = ogUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
                    BaseConfidence = 0.52
                });
            }

            return results;
        }

        internal List<string> ParseManifestLinks(string html, string baseUrl)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(html))
                return results;

            string head = ExtractHead(html);
            string resolvedBase = ResolveBaseTag(head, baseUrl);

            var manifestPattern = new Regex(
                @"<link\b[^>]*\brel\s*=\s*[""'][^""']*manifest[^""']*[""'][^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var hrefPattern = new Regex(
                @"\bhref\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);

            foreach (Match linkMatch in manifestPattern.Matches(head))
            {
                var hrefMatch = hrefPattern.Match(linkMatch.Value);
                if (!hrefMatch.Success)
                    continue;

                string manifestUrl = ResolveUrl(hrefMatch.Groups[1].Value, resolvedBase);
                if (manifestUrl != null)
                    results.Add(manifestUrl);
            }

            return results
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal List<DiscoveredIconLink> ParseManifestIcons(string manifestJson, string manifestUrl)
        {
            var results = new List<DiscoveredIconLink>();
            if (string.IsNullOrWhiteSpace(manifestJson))
                return results;

            var objectPattern = new Regex(@"\{[^{}]*""src""\s*:\s*""(?<src>[^""]+)""[^{}]*\}",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in objectPattern.Matches(manifestJson))
            {
                string objectText = match.Value;
                string src = match.Groups["src"].Value;
                string resolved = ResolveUrl(src, manifestUrl);
                if (resolved == null)
                    continue;

                int size = 192;
                var sizeMatch = Regex.Match(objectText, @"""sizes""\s*:\s*""(?<sizes>[^""]+)""",
                    RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    int parsed = ParseLargestSize(sizeMatch.Groups["sizes"].Value);
                    if (parsed > 0) size = parsed;
                }

                bool isSvg = resolved.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                             Regex.IsMatch(objectText, @"image/svg\+xml", RegexOptions.IgnoreCase);

                results.Add(new DiscoveredIconLink
                {
                    Url = resolved,
                    Size = size,
                    Priority = isSvg ? 3 : 2,
                    Tier = IconTier.SiteCanonical,
                    SourceType = "manifest-icon",
                    IsSvgHint = isSvg,
                    BaseConfidence = isSvg ? 0.68 : 0.91
                });
            }

            return results;
        }

        private List<DiscoveredIconLink> BuildWellKnownCandidates(string origin)
        {
            var results = new List<DiscoveredIconLink>();
            foreach (string path in WellKnownPaths)
            {
                results.Add(new DiscoveredIconLink
                {
                    Url = origin.TrimEnd('/') + path,
                    Size = path.IndexOf("apple-touch", StringComparison.OrdinalIgnoreCase) >= 0 ? 180 : 32,
                    Priority = path.IndexOf("apple-touch", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 3,
                    Tier = IconTier.SiteCanonical,
                    SourceType = "well-known",
                    IsSvgHint = false,
                    BaseConfidence = path.IndexOf("apple-touch", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.90 : 0.84
                });
            }
            return results;
        }

        private static string ExtractHead(string html)
        {
            string head = html;
            var headMatch = Regex.Match(html, @"<head[^>]*>(.*?)</head>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (headMatch.Success)
                head = headMatch.Groups[1].Value;

            head = Regex.Replace(head, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            head = Regex.Replace(head, @"<script[^>]*>.*?</script>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            head = Regex.Replace(head, @"<style[^>]*>.*?</style>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return head;
        }

        private static string ResolveBaseTag(string head, string fallbackBase)
        {
            string resolvedBase = fallbackBase;
            var baseMatch = Regex.Match(head, @"<base[^>]+href\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (!baseMatch.Success)
                return resolvedBase;

            string candidate = baseMatch.Groups[1].Value.Trim();
            try
            {
                var fallbackUri = new Uri(fallbackBase);
                var candidateUri = new Uri(fallbackUri, candidate);
                if (candidateUri.Host.Equals(fallbackUri.Host, StringComparison.OrdinalIgnoreCase))
                    resolvedBase = candidateUri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                Logger.Debug("ResolveBaseTag", ex);
            }

            return resolvedBase;
        }

        private string ParseOgImage(string head, string baseUrl)
        {
            var ogPattern = new Regex(
                @"<meta\b[^>]*\bproperty\s*=\s*[""']?og:image[""']?[^>]*\bcontent\s*=\s*[""']?([^""'\s>]+)",
                RegexOptions.IgnoreCase);

            var match = ogPattern.Match(head);
            if (!match.Success)
            {
                ogPattern = new Regex(
                    @"<meta\b[^>]*\bcontent\s*=\s*[""']?([^""'\s>]+)[^>]*\bproperty\s*=\s*[""']?og:image[""']?",
                    RegexOptions.IgnoreCase);
                match = ogPattern.Match(head);
            }

            if (!match.Success)
                return null;

            return ResolveUrl(match.Groups[1].Value, baseUrl);
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
                var baseUri = new Uri(baseUrl);
                var resolved = new Uri(baseUri, href);
                return resolved.AbsoluteUri;
            }
            catch (Exception ex)
            {
                Logger.Debug("ResolveUrl", ex);
                return null;
            }
        }

        private static string GetCanonicalCandidateUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                return url;

            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty
            };

            if ((builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                 builder.Port == 443) ||
                (builder.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                 builder.Port == 80))
            {
                builder.Port = -1;
            }

            return builder.Uri.AbsoluteUri;
        }

        private static int ParseLargestSize(string sizesValue)
        {
            if (string.IsNullOrWhiteSpace(sizesValue))
                return 0;

            int max = 0;
            var tokens = sizesValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                var m = Regex.Match(token, @"(\d+)x(\d+)", RegexOptions.IgnoreCase);
                if (!m.Success)
                    continue;

                int w, h;
                if (int.TryParse(m.Groups[1].Value, out w) &&
                    int.TryParse(m.Groups[2].Value, out h))
                {
                    max = Math.Max(max, Math.Max(w, h));
                }
            }

            return max;
        }

        private IconCandidate BuildCandidate(string targetHost, DiscoveredIconLink link, DownloadResult download)
        {
            byte[] data = download.Data;
            if (data == null || data.Length == 0)
                return null;

            bool isSvg = link.IsSvgHint || LooksLikeSvg(link.Url, download.ContentType, data);
            if (isSvg)
            {
                return new IconCandidate
                {
                    ProviderName = Name,
                    TargetHost = targetHost,
                    SourceUrl = link.Url,
                    Tier = link.Tier,
                    RawData = data,
                    NormalizedPngData = null,
                    OriginalFormat = "svg",
                    Width = 0,
                    Height = 0,
                    IsSvg = true,
                    IsSynthetic = false,
                    IsPlaceholderSuspected = false,
                    IsBlankSuspected = false,
                    ConfidenceScore = Math.Max(0.05, link.BaseConfidence - 0.40),
                    Notes = "SVG candidate detected from " + link.SourceType + "."
                };
            }

            if (!Util.IsValidImage(data))
                return null;

            byte[] normalized = Util.NormalizeToPng(data);
            byte[] normalizedPng = normalized ?? data;

            int width;
            int height;
            string format;
            Util.TryGetImageInfo(normalizedPng, out width, out height, out format);

            bool blank = Util.IsProbablyBlankImage(normalizedPng);
            double score = link.BaseConfidence + ComputeSizeScore(width, height);
            if (blank)
                score -= 0.30;
            score = Math.Max(0.0, Math.Min(1.0, score));

            return new IconCandidate
            {
                ProviderName = Name,
                TargetHost = targetHost,
                SourceUrl = link.Url,
                Tier = link.Tier,
                RawData = data,
                NormalizedPngData = normalizedPng,
                OriginalFormat = string.IsNullOrEmpty(format) ? "unknown" : format,
                Width = width,
                Height = height,
                IsSvg = false,
                IsSynthetic = false,
                IsPlaceholderSuspected = false,
                IsBlankSuspected = blank,
                ConfidenceScore = score,
                Notes = link.SourceType
            };
        }

        private static double ComputeSizeScore(int width, int height)
        {
            int max = Math.Max(width, height);
            if (max <= 0) return 0.0;
            if (max >= 192) return 0.15;
            if (max >= 128) return 0.10;
            if (max >= 64) return 0.06;
            if (max >= 32) return 0.03;
            return 0.01;
        }

        private async Task<DownloadResult> DownloadDataAsync(string url, int timeoutMs, long maxBytes,
            bool allowPrivateResponse, CancellationToken token = default(CancellationToken))
        {
            int attempt = 0;
            Uri responseUri = null;

            while (attempt < 2)
            {
                bool shouldRetry = false;

                try
                {
                    token.ThrowIfCancellationRequested();

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        request.Headers.Add("Accept", "text/html,application/manifest+json,application/json,image/svg+xml,image/webp,image/apng,image/*,*/*;q=0.8");
                        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            cts.CancelAfter(Math.Max(1000, timeoutMs));
                            var response = await SharedHttp.Instance.SendAsync(request,
                                HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                            if (!response.IsSuccessStatusCode)
                            {
                                shouldRetry = attempt == 0 && IsRetryableStatus(response.StatusCode);
                                if (!shouldRetry)
                                {
                                    return new DownloadResult
                                    {
                                        Data = null,
                                        ResponseUri = null,
                                        ContentType = null,
                                        StatusCode = response.StatusCode
                                    };
                                }
                                continue;
                            }

                            responseUri = response.RequestMessage != null ? response.RequestMessage.RequestUri : null;
                            if (!allowPrivateResponse && responseUri != null && Util.IsPrivateHost(responseUri.Host))
                            {
                                return new DownloadResult
                                {
                                    Data = null,
                                    ResponseUri = responseUri,
                                    ContentType = null,
                                    StatusCode = response.StatusCode
                                };
                            }

                            var contentLength = response.Content.Headers.ContentLength;
                            if (contentLength.HasValue && contentLength.Value > maxBytes)
                            {
                                return new DownloadResult
                                {
                                    Data = null,
                                    ResponseUri = responseUri,
                                    ContentType = null,
                                    StatusCode = response.StatusCode
                                };
                            }

                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var ms = new MemoryStream())
                            {
                                if (stream == null)
                                {
                                    return new DownloadResult
                                    {
                                        Data = null,
                                        ResponseUri = responseUri,
                                        ContentType = response.Content.Headers.ContentType != null
                                            ? response.Content.Headers.ContentType.MediaType
                                            : null,
                                        StatusCode = response.StatusCode
                                    };
                                }

                                byte[] buffer = new byte[8192];
                                int read;
                                long total = 0;
                                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                                {
                                    await ms.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                    total += read;
                                    if (total > maxBytes)
                                    {
                                        return new DownloadResult
                                        {
                                            Data = null,
                                            ResponseUri = responseUri,
                                            ContentType = response.Content.Headers.ContentType != null
                                                ? response.Content.Headers.ContentType.MediaType
                                                : null,
                                            StatusCode = response.StatusCode
                                        };
                                    }
                                }

                                return new DownloadResult
                                {
                                    Data = ms.ToArray(),
                                    ResponseUri = responseUri,
                                    ContentType = response.Content.Headers.ContentType != null
                                        ? response.Content.Headers.ContentType.MediaType
                                        : null,
                                    StatusCode = response.StatusCode
                                };
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        throw;
                    shouldRetry = attempt == 0;
                }
                catch (HttpRequestException ex)
                {
                    Logger.Debug("DownloadDataAsync", ex);
                    shouldRetry = attempt == 0;
                }
                catch (IOException ex)
                {
                    Logger.Debug("DownloadDataAsync", ex);
                    shouldRetry = attempt == 0;
                }
                catch (Exception ex)
                {
                    Logger.Debug("DownloadDataAsync", ex);
                    return new DownloadResult { Data = null, ResponseUri = responseUri, ContentType = null, StatusCode = null };
                }
                finally
                {
                    attempt++;
                }

                if (shouldRetry && attempt < 2)
                {
                    await Task.Delay(150 + NextJitter(250), token).ConfigureAwait(false);
                    continue;
                }

                break;
            }

            return new DownloadResult { Data = null, ResponseUri = responseUri, ContentType = null, StatusCode = null };
        }

        private static bool IsRetryableStatus(HttpStatusCode statusCode)
        {
            return statusCode == (HttpStatusCode)429 ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        private static int NextJitter(int maxExclusive)
        {
            lock (RetryJitterLock)
            {
                return RetryJitter.Next(0, Math.Max(1, maxExclusive));
            }
        }

        private static string DecodeText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            Encoding encoding = Encoding.UTF8;
            string text = encoding.GetString(data);
            string headSample = text.Substring(0, Math.Min(text.Length, 4096));
            var charsetMatch = Regex.Match(headSample,
                @"charset\s*=\s*[""']?([^""'\s;>]+)", RegexOptions.IgnoreCase);
            if (charsetMatch.Success)
            {
                try
                {
                    encoding = Encoding.GetEncoding(charsetMatch.Groups[1].Value.Trim());
                    text = encoding.GetString(data);
                }
                catch (Exception ex)
                {
                    Logger.Debug("DecodeText", ex);
                }
            }
            return text;
        }

        private static bool LooksLikeSvg(string sourceUrl, string contentType, byte[] data)
        {
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrEmpty(sourceUrl) &&
                sourceUrl.IndexOf(".svg", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            int sampleLength = Math.Min(data.Length, 512);
            string sample = Encoding.UTF8.GetString(data, 0, sampleLength);
            return sample.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal sealed class DiscoveredIconLink
        {
            public string Url { get; set; }
            public int Size { get; set; }
            public int Priority { get; set; }
            public IconTier Tier { get; set; }
            public string SourceType { get; set; }
            public bool IsSvgHint { get; set; }
            public double BaseConfidence { get; set; }
        }

        private sealed class DownloadResult
        {
            public byte[] Data { get; set; }
            public Uri ResponseUri { get; set; }
            public string ContentType { get; set; }
            public HttpStatusCode? StatusCode { get; set; }
        }

        private sealed class CandidateDownloadOutcome
        {
            public CandidateDownloadOutcome(int order, IconCandidate candidate)
            {
                Order = order;
                Candidate = candidate;
            }

            public int Order { get; private set; }
            public IconCandidate Candidate { get; private set; }
        }
    }
}
