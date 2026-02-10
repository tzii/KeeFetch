using System;
using System.Net;
using System.Threading;

namespace KeeFetch.IconProviders
{
    internal sealed class DuckDuckGoProvider : IconProviderBase
    {
        public override string Name => "DuckDuckGo";

        public override byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return null;

            string url = string.Format(
                "https://icons.duckduckgo.com/ip3/{0}.ico",
                Uri.EscapeDataString(host));

            return DownloadBytes(url, timeoutMs, proxy, token);
        }
    }
}
