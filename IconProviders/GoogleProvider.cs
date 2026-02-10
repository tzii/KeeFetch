using System;
using System.IO;
using System.Net;

namespace KeeFetch.IconProviders
{
    internal sealed class GoogleProvider : IIconProvider
    {
        public string Name => "Google";

        public byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy)
        {
            if (Util.IsPrivateHost(host))
                return null;

            string url = string.Format(
                "https://www.google.com/s2/favicons?domain={0}&sz={1}",
                Uri.EscapeDataString(host), size);

            return DownloadBytes(url, timeoutMs, proxy);
        }

        private byte[] DownloadBytes(string url, int timeoutMs, IWebProxy proxy)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs * 2;
                request.AllowAutoRedirect = true;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                if (proxy != null) request.Proxy = proxy;

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
                        ms.Write(buffer, 0, read);
                        total += read;
                        if (total > 512 * 1024)
                            return null;
                    }
                    byte[] data = ms.ToArray();
                    return Util.IsValidImage(data) ? data : null;
                }
            }
            catch { return null; }
        }
    }
}
