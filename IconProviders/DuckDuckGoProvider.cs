using System;
using System.IO;
using System.Net;

namespace KeeFetch.IconProviders
{
    internal sealed class DuckDuckGoProvider : IIconProvider
    {
        public string Name => "DuckDuckGo";

        public byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy)
        {
            string url = string.Format(
                "https://icons.duckduckgo.com/ip3/{0}.ico",
                Uri.EscapeDataString(host));

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
                    stream.CopyTo(ms);
                    byte[] data = ms.ToArray();
                    return Util.IsValidImage(data) ? data : null;
                }
            }
            catch { return null; }
        }
    }
}
