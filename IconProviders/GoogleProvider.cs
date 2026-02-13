using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    internal sealed class GoogleProvider : IconProviderBase
    {
        public override string Name { get { return "Google"; } }

        public override Task<byte[]> GetIconAsync(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return Task.FromResult<byte[]>(null);

            string url = string.Format(
                "https://www.google.com/s2/favicons?domain={0}&sz={1}",
                Uri.EscapeDataString(host), size);

            return DownloadBytesAsync(url, timeoutMs, proxy, token);
        }
    }
}
