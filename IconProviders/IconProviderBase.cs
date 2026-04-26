using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeeFetch.IconSelection;

namespace KeeFetch.IconProviders
{
    /// <summary>
    /// Shared HTTP download logic for resolver providers.
    /// </summary>
    internal abstract class IconProviderBase : IIconProvider
    {
        /// <summary>Maximum size for a downloaded icon in bytes (512 KB).</summary>
        protected const long MaxIconDownloadBytes = 512 * 1024;

        private const string UserAgentString =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        private static readonly Random RetryJitter = new Random();
        private static readonly object RetryJitterLock = new object();

        public abstract string Name { get; }
        public abstract ProviderCapabilities Capabilities { get; }
        protected abstract string BuildRequestUrl(IconRequest request);

        /// <summary>
        /// Returns at most one candidate for single-endpoint providers.
        /// </summary>
        public virtual async Task<IReadOnlyList<IconCandidate>> GetCandidatesAsync(IconRequest request,
            CancellationToken token = default(CancellationToken))
        {
            var empty = new List<IconCandidate>(0);
            if (request == null || string.IsNullOrWhiteSpace(request.TargetHost))
                return empty;

            if (!Capabilities.AllowPrivateHosts && Util.IsPrivateHost(request.TargetHost))
                return empty;

            string requestUrl = BuildRequestUrl(request);
            if (string.IsNullOrWhiteSpace(requestUrl))
                return empty;

            var candidate = await DownloadCandidateAsync(requestUrl, request, token).ConfigureAwait(false);
            if (candidate == null)
                return empty;

            return new List<IconCandidate> { candidate };
        }

        protected async Task<IconCandidate> DownloadCandidateAsync(string url, IconRequest request,
            CancellationToken token = default(CancellationToken))
        {
            int attempt = 0;
            int timeoutMs = Math.Max(1000, request.TimeoutMs);

            while (attempt < 2)
            {
                bool shouldRetry = false;
                HttpStatusCode? statusCode = null;
                string contentType = null;

                try
                {
                    token.ThrowIfCancellationRequested();

                    using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        httpRequest.Headers.Add("User-Agent", UserAgentString);
                        httpRequest.Headers.Add("Accept", "image/svg+xml,image/webp,image/apng,image/*,*/*;q=0.8");

                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            cts.CancelAfter(timeoutMs);
                            var response = await SharedHttp.Instance.SendAsync(httpRequest,
                                HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                            statusCode = response.StatusCode;
                            contentType = response.Content.Headers.ContentType != null
                                ? response.Content.Headers.ContentType.MediaType
                                : null;

                            if (!response.IsSuccessStatusCode)
                            {
                                shouldRetry = attempt == 0 && IsRetryableStatus(response.StatusCode);
                                if (!shouldRetry)
                                    return null;
                                continue;
                            }

                            var contentLength = response.Content.Headers.ContentLength;
                            if (contentLength.HasValue && contentLength.Value > MaxIconDownloadBytes)
                                return null;

                            byte[] data;
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var ms = new MemoryStream())
                            {
                                if (stream == null)
                                    return null;

                                byte[] buffer = new byte[8192];
                                int read;
                                long total = 0;

                                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                                {
                                    await ms.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                    total += read;
                                    if (total > MaxIconDownloadBytes)
                                        return null;
                                }

                                data = ms.ToArray();
                            }

                            return BuildCandidateFromData(url, request, data, contentType);
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
                    Logger.Debug(Name, ex);
                    shouldRetry = attempt == 0;
                }
                catch (IOException ex)
                {
                    Logger.Debug(Name, ex);
                    shouldRetry = attempt == 0;
                }
                catch (Exception ex)
                {
                    Logger.Warn(Name, ex);
                    return null;
                }
                finally
                {
                    attempt++;
                }

                if (shouldRetry && attempt < 2)
                {
                    int delayMs = IsRetryableStatus(statusCode) ? 250 + NextJitter(350) : 150 + NextJitter(250);
                    await Task.Delay(delayMs, token).ConfigureAwait(false);
                    continue;
                }

                break;
            }

            return null;
        }

        private IconCandidate BuildCandidateFromData(string sourceUrl, IconRequest request, byte[] data, string contentType)
        {
            if (data == null || data.Length == 0)
                return null;

            bool isSvg = LooksLikeSvg(sourceUrl, contentType, data);
            if (isSvg)
            {
                return new IconCandidate
                {
                    ProviderName = Name,
                    TargetHost = request.TargetHost,
                    SourceUrl = sourceUrl,
                    Tier = Capabilities.DefaultTier,
                    RawData = data,
                    NormalizedPngData = null,
                    OriginalFormat = "svg",
                    Width = 0,
                    Height = 0,
                    IsSvg = true,
                    IsSynthetic = Capabilities.IsSyntheticCapable,
                    IsPlaceholderSuspected = Capabilities.IsPlaceholderProne,
                    IsBlankSuspected = false,
                    ConfidenceScore = Math.Max(0.05, Capabilities.BaseConfidence - 0.45),
                    Notes = "SVG detected; provider returned non-raster payload."
                };
            }

            if (!Util.IsValidImage(data))
                return null;

            var normalized = Util.NormalizeToPng(data);
            byte[] normalizedPng = normalized ?? data;

            int width;
            int height;
            string format;
            Util.TryGetImageInfo(normalizedPng, out width, out height, out format);

            bool isBlank = Util.IsProbablyBlankImage(normalizedPng);
            double score = Capabilities.BaseConfidence + ComputeSizeScore(width, height);
            if (isBlank)
                score -= 0.35;

            score = Math.Max(0.0, Math.Min(1.0, score));

            return new IconCandidate
            {
                ProviderName = Name,
                TargetHost = request.TargetHost,
                SourceUrl = sourceUrl,
                Tier = Capabilities.DefaultTier,
                RawData = data,
                NormalizedPngData = normalizedPng,
                OriginalFormat = string.IsNullOrEmpty(format) ? "unknown" : format,
                Width = width,
                Height = height,
                IsSvg = false,
                IsSynthetic = Capabilities.IsSyntheticCapable,
                IsPlaceholderSuspected = Capabilities.IsPlaceholderProne ||
                    (Capabilities.IsSyntheticCapable && isBlank),
                IsBlankSuspected = isBlank,
                ConfidenceScore = score,
                Notes = isBlank ? "Image appears visually blank." : string.Empty
            };
        }

        private static double ComputeSizeScore(int width, int height)
        {
            int max = Math.Max(width, height);
            if (max <= 0) return 0.0;
            if (max >= 192) return 0.16;
            if (max >= 128) return 0.12;
            if (max >= 64) return 0.08;
            if (max >= 32) return 0.04;
            return 0.01;
        }

        private static bool IsRetryableStatus(HttpStatusCode? statusCode)
        {
            if (!statusCode.HasValue)
                return false;

            return statusCode.Value == (HttpStatusCode)429 ||
                   statusCode.Value == HttpStatusCode.ServiceUnavailable ||
                   statusCode.Value == HttpStatusCode.GatewayTimeout ||
                   statusCode.Value == HttpStatusCode.BadGateway;
        }

        private static int NextJitter(int maxExclusive)
        {
            lock (RetryJitterLock)
            {
                return RetryJitter.Next(0, Math.Max(1, maxExclusive));
            }
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
    }
}
