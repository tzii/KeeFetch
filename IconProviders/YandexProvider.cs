using System;
using System.Net;
using System.Threading;

namespace KeeFetch.IconProviders
{
    internal sealed class YandexProvider : IconProviderBase
    {
        public override string Name => "Yandex";

        public override byte[] GetIcon(string host, int size, int timeoutMs, IWebProxy proxy,
            CancellationToken token = default(CancellationToken))
        {
            if (Util.IsPrivateHost(host))
                return null;

            string resolvedSize = "l";
            if (size <= 16) resolvedSize = "s";
            else if (size <= 32) resolvedSize = "m";

            string url = string.Format(
                "https://favicon.yandex.net/favicon/{0}?size={1}",
                Uri.EscapeDataString(host), resolvedSize);

            return DownloadBytes(url, timeoutMs, proxy, token);
        }
    }
}
