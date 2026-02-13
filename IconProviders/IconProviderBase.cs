using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    /// <summary>
    /// Shared HTTP download logic for fallback icon providers.
    /// Subclasses only need to build the URL and call <see cref="DownloadBytesAsync"/>.
    /// </summary>
    internal abstract class IconProviderBase : IIconProvider
    {
        /// <summary>Maximum size for a downloaded icon in bytes (512 KB).</summary>
        protected const long MaxIconDownloadBytes = 512 * 1024;

        private const string UserAgentString =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        public abstract string Name { get; }
        public abstract Task<byte[]> GetIconAsync(string host, int size, int timeoutMs,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Downloads bytes from <paramref name="url"/> with a size cap of <see cref="MaxIconDownloadBytes"/>,
        /// validates the result as an image, and supports cancellation via <paramref name="token"/>.
        /// </summary>
        protected async Task<byte[]> DownloadBytesAsync(string url, int timeoutMs,
            CancellationToken token = default(CancellationToken))
        {
            try
            {
                token.ThrowIfCancellationRequested();

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("User-Agent", UserAgentString);
                    request.Headers.Add("Accept", "image/webp,image/apng,image/*,*/*;q=0.8");

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(timeoutMs);
                        var response = await SharedHttp.Instance.SendAsync(request,
                            HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                            return null;

                        var contentLength = response.Content.Headers.ContentLength;
                        if (contentLength.HasValue && contentLength.Value > MaxIconDownloadBytes)
                            return null;

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var ms = new MemoryStream())
                        {
                            if (stream == null) return null;
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
                            byte[] data = ms.ToArray();
                            return Util.IsValidImage(data) ? data : null;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Warn("DownloadBytesAsync", ex); return null; }
        }
    }
}
