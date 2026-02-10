using System;
using System.Net;
using System.Threading;

namespace KeeFetch.IconProviders
{
    internal sealed class GoogleProvider : IconProviderBase
    {
        public override string Name => "Google";

        public override byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return null;

            string url = string.Format(
                "https://www.google.com/s2/favicons?domain={0}&sz={1}",
                Uri.EscapeDataString(host), size);

            return DownloadBytes(url, timeoutMs, proxy, token);
        }
    }
}
