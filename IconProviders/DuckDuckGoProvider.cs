using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    internal sealed class DuckDuckGoProvider : IconProviderBase
    {
        public override string Name => "DuckDuckGo";

        public override Task<byte[]> GetIconAsync(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return Task.FromResult<byte[]>(null);

            string url = string.Format(
                "https://icons.duckduckgo.com/ip3/{0}.ico",
                Uri.EscapeDataString(host));

            return DownloadBytesAsync(url, timeoutMs, proxy, token);
        }
    }
}
