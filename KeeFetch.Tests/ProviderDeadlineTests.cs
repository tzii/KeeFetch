using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    /// <summary>
    /// Deterministic deadline tests over a fake HTTP transport: no public
    /// internet endpoints are required. A stalled response stream that ignores
    /// the cancellation token reproduces the .NET Framework behavior that
    /// previously allowed indefinite hangs.
    /// </summary>
    [TestClass]
    public class ProviderDeadlineTests
    {
        private static byte[] twoTonePng;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            using (var bitmap = new Bitmap(16, 16))
            {
                for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                {
                    Color color;
                    if (x < 8 && y < 8) color = Color.White;
                    else if (x >= 8 && y < 8) color = Color.Black;
                    else if (x < 8) color = Color.Firebrick;
                    else color = Color.SteelBlue;
                    bitmap.SetPixel(x, y, color);
                }
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    twoTonePng = ms.ToArray();
                }
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            SharedHttp.ResetClientForTests();
            FaviconDownloader.ClearCache();
        }

        private sealed class StalledStream : Stream
        {
            private readonly TaskCompletionSource<object> released =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            private volatile bool disposed;

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            // Mimics a .NET Framework response stream: the cancellation token
            // is ignored; only disposal of the stream unblocks the read.
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                await released.Task.ConfigureAwait(false);
                if (disposed)
                    throw new ObjectDisposedException("StalledStream");
                return 0;
            }

            public override void Flush() { throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

            protected override void Dispose(bool disposing)
            {
                disposed = true;
                released.TrySetResult(null);
                base.Dispose(disposing);
            }
        }

        private sealed class RoutingHandler : HttpMessageHandler
        {
            public readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> RequestCounts =
                new System.Collections.Concurrent.ConcurrentDictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;

            public RoutingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                this.send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                string key = request.RequestUri != null ? request.RequestUri.Host : "unknown";
                RequestCounts.AddOrUpdate(key, 1, (_, n) => n + 1);
                return send(request, cancellationToken);
            }
        }

        private static Task<HttpResponseMessage> HeadersThenStall(HttpRequestMessage request,
            CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StalledStream())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }

        private static async Task<HttpResponseMessage> StallBeforeHeaders(HttpRequestMessage request,
            CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            return null;
        }

        private static Task<HttpResponseMessage> SuccessPng(HttpRequestMessage request,
            CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(twoTonePng)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }

        private static Task<HttpResponseMessage> ServiceUnavailable(HttpRequestMessage request,
            CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new ByteArrayContent(new byte[0])
            };
            return Task.FromResult(response);
        }

        private static Task<HttpResponseMessage> NetworkFailure(HttpRequestMessage request,
            CancellationToken token)
        {
            throw new HttpRequestException("simulated connection failure");
        }

        private static Configuration CustomPolicyConfig(string providerOrderCsv,
            int primaryMs, int fallbackMs, int cumulativeMs, bool stopAfterStrongResolved)
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace);
            config.FetchProfileId = "custom";

            string[] enabled = providerOrderCsv.Split(',');
            foreach (string providerName in FaviconDownloader.DefaultProviderOrder)
                config.SetProviderEnabled(providerName, enabled.Contains(providerName.Trim()));

            config.ProviderOrder = providerOrderCsv;
            config.UseThirdPartyFallbacks = true;
            config.AllowSyntheticFallbacks = false;

            ace.SetLong("KeeFetch.CustomPrimaryTimeoutMs", primaryMs);
            ace.SetLong("KeeFetch.CustomFallbackTimeoutMs", fallbackMs);
            ace.SetLong("KeeFetch.CustomCumulativeTimeoutMs", cumulativeMs);
            ace.SetLong("KeeFetch.CustomStopAfterStrongResolved", stopAfterStrongResolved ? 1 : 0);
            return config;
        }

        private static RoutingHandler currentHandler;

        private static void InstallTransport(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            currentHandler = new RoutingHandler(send);
            SharedHttp.ReplaceClientForTests(new HttpClient(currentHandler));
        }

        [TestMethod]
        public async Task StalledResponseStream_BecomesProviderTimeoutWithinBudget()
        {
            InstallTransport(HeadersThenStall);
            var config = CustomPolicyConfig("Google", 1200, 800, 6000, false);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("https://stall-timeout.example/");
            stopwatch.Stop();

            Assert.AreEqual(FaviconStatus.NotFound, result.Status);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1200 + 2000,
                "fetch must stay close to the 1200ms provider budget, took " + stopwatch.ElapsedMilliseconds + "ms");
            var googleMetric = result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google");
            Assert.IsNotNull(googleMetric,
                "Google provider must appear in metrics. Status=" + result.Status +
                " Metrics=[" + string.Join("; ", result.ProviderMetrics.Select(
                    m => m.ProviderName + "=" + m.Outcome).ToArray()) + "]" +
                " Diagnostics=" + result.DiagnosticsSummary);
            Assert.AreEqual("timeout", googleMetric.Outcome,
                "deadline abort must surface as timeout, got " + googleMetric.Outcome);
        }

        [TestMethod]
        public async Task StallBeforeHeaders_BecomesProviderTimeout()
        {
            InstallTransport(StallBeforeHeaders);
            var config = CustomPolicyConfig("Google", 1200, 800, 6000, false);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("https://stall-headers.example/");
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1200 + 2000,
                "header-phase stall must honor the deadline, took " + stopwatch.ElapsedMilliseconds + "ms");
            var googleMetric = result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google");
            Assert.IsNotNull(googleMetric);
            Assert.AreEqual("timeout", googleMetric.Outcome);
        }

        [TestMethod]
        public async Task CumulativeDeadline_IsHardCeilingAndSurfacedSeparately()
        {
            InstallTransport(HeadersThenStall);
            // Direct Site burns its 1200ms primary budget, leaving less than
            // the minimum provider slice, so Google must never start.
            var config = CustomPolicyConfig("Direct Site,Google", 1200, 800, 1500, false);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("https://stall-cumulative.example/");
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1500 + 2500,
                "cumulative budget must cap the pipeline, took " + stopwatch.ElapsedMilliseconds + "ms");
            Assert.IsNull(result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google"),
                "no provider may start after the cumulative budget is exhausted");
            Assert.IsNotNull(result.ProviderMetrics.FirstOrDefault(
                    m => m.ProviderName == "Pipeline" && m.Outcome == "timeout"),
                "cumulative exhaustion must be surfaced as an explicit pipeline timeout");
            Assert.IsTrue(result.DiagnosticsSummary.Contains("cumulative-budget-exhausted"),
                "diagnostics must distinguish budget exhaustion from not-found");
        }

        [TestMethod]
        public async Task ProviderRetries_CannotExtendBeyondOneProviderBudget()
        {
            int requests = 0;
            InstallTransport((request, token) =>
            {
                int n = Interlocked.Increment(ref requests);
                // First attempt fails fast with a retryable status; the retry
                // then stalls, so only the single provider deadline bounds it.
                return n == 1 ? ServiceUnavailable(request, token) : HeadersThenStall(request, token);
            });
            var config = CustomPolicyConfig("Google", 1500, 800, 8000, false);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("https://stall-retry.example/");
            stopwatch.Stop();

            Assert.IsTrue(requests >= 2, "the retry must actually have started");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2600,
                "retries must stay inside one 1500ms budget (2x budget = 3000ms minimum without the cap), took "
                + stopwatch.ElapsedMilliseconds + "ms");
            var googleMetric = result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google");
            Assert.IsNotNull(googleMetric);
            Assert.AreEqual("timeout", googleMetric.Outcome);
        }

        [TestMethod]
        public async Task SuccessfulResponse_StillProducesSuccessResult()
        {
            InstallTransport(SuccessPng);
            var config = CustomPolicyConfig("Google", 4000, 2000, 8000, false);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://success.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.AreEqual("Google", result.Provider);
            var googleMetric = result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google");
            Assert.IsNotNull(googleMetric);
            Assert.AreEqual("candidate", googleMetric.Outcome);
        }

        [TestMethod]
        public async Task NetworkFailure_IsProviderErrorNotTimeout()
        {
            InstallTransport(NetworkFailure);
            var config = CustomPolicyConfig("Google", 4000, 2000, 8000, false);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://network-failure.example/");

            var googleMetric = result.ProviderMetrics.FirstOrDefault(m => m.ProviderName == "Google");
            Assert.IsNotNull(googleMetric);
            Assert.AreEqual("error", googleMetric.Outcome,
                "real transport errors must stay distinguishable from timeouts");
        }

        [TestMethod]
        public async Task ExternalCancellation_RemainsCancellation()
        {
            InstallTransport(HeadersThenStall);
            var config = CustomPolicyConfig("Google", 20000, 8000, 60000, false);
            var downloader = new FaviconDownloader(config);

            using (var cts = new CancellationTokenSource(400))
            {
                try
                {
                    await downloader.DownloadAsync("https://external-cancel.example/", cts.Token);
                    Assert.Fail("external cancellation must propagate");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [TestMethod]
        public async Task StopAfterStrongResolved_SkipsRemainingProviders()
        {
            InstallTransport((request, token) =>
            {
                string host = request.RequestUri.Host;
                if (host.IndexOf("twenty-icons", StringComparison.OrdinalIgnoreCase) >= 0)
                    return SuccessPng(request, token);
                return HeadersThenStall(request, token);
            });

            var config = CustomPolicyConfig("Twenty Icons,Google", 1500, 1500, 8000, true);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("https://stop-early.example/");
            stopwatch.Stop();

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.AreEqual("Twenty Icons", result.Provider);
            int googleRequests;
            currentHandler.RequestCounts.TryGetValue("www.google.com", out googleRequests);
            var selected = result.Selection != null ? result.Selection.SelectedCandidate : null;
            Assert.AreEqual(0, googleRequests,
                "early-stop policy must prevent the next provider from being queried. Selected: tier="
                + (selected != null ? selected.Tier.ToString() : "null")
                + " conf=" + (selected != null ? selected.ConfidenceScore.ToString() : "-")
                + " blank=" + (selected != null ? selected.IsBlankSuspected.ToString() : "-")
                + " svg=" + (selected != null ? selected.IsSvg.ToString() : "-")
                + " placeholder=" + (selected != null ? selected.IsPlaceholderSuspected.ToString() : "-")
                + " synthetic=" + (selected != null ? selected.IsSynthetic.ToString() : "-")
                + " provider=" + (selected != null ? selected.ProviderName : "-")
                + " attempted=[" + string.Join(";", result.AttemptedProviders.ToArray()) + "]");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1500 + 2000,
                "run must end after the strong resolver, took " + stopwatch.ElapsedMilliseconds + "ms");
        }

        [TestMethod]
        public async Task StopAfterStrongResolvedDisabled_QueriesRemainingProviders()
        {
            InstallTransport((request, token) =>
            {
                string host = request.RequestUri.Host;
                if (host.IndexOf("twenty-icons", StringComparison.OrdinalIgnoreCase) >= 0)
                    return SuccessPng(request, token);
                return SuccessPng(request, token);
            });

            var config = CustomPolicyConfig("Twenty Icons,Google", 1500, 1500, 8000, false);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://no-stop.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.IsTrue(currentHandler.RequestCounts.ContainsKey("www.google.com"),
                "without early stop the next provider must still be queried");
        }
    }
}
