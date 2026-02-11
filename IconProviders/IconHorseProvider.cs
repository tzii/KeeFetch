using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KeeFetch.IconProviders
{
    internal sealed class IconHorseProvider : IconProviderBase
    {
        public override string Name => "Icon Horse";

        public override Task<byte[]> GetIconAsync(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return Task.FromResult<byte[]>(null);

            string url = string.Format(
                "https://icon.horse/icon/{0}",
                Uri.EscapeDataString(host));

            return DownloadBytesAsync(url, timeoutMs, proxy, token);
        }
    }
}
