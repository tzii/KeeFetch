using System;
using System.IO;
using System.Net;
using System.Threading;

namespace KeeFetch.IconProviders
{
    /// <summary>
    /// Shared HTTP download logic for fallback icon providers.
    /// Subclasses only need to build the URL and call <see cref="DownloadBytes"/>.
    /// </summary>
    internal abstract class IconProviderBase : IIconProvider
    {
        public abstract string Name { get; }
        public abstract byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Downloads bytes from <paramref name="url"/> with a size cap of 512 KB,
        /// validates the result as an image, and supports cancellation via <paramref name="token"/>.
        /// </summary>
        protected byte[] DownloadBytes(string url, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs * 2;
                request.AllowAutoRedirect = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                if (proxy != null) request.Proxy = proxy;

                // Wire cancellation to abort the request
                using (token.Register(() => request.Abort(), useSynchronizationContext: false))
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    if (stream == null) return null;
                    byte[] buffer = new byte[8192];
                    int read;
                    long total = 0;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        ms.Write(buffer, 0, read);
                        total += read;
                        if (total > 512 * 1024)
                            return null;
                    }
                    byte[] data = ms.ToArray();
                    return Util.IsValidImage(data) ? data : null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Logger.Warn("DownloadBytes", ex); return null; }
        }
    }
}
