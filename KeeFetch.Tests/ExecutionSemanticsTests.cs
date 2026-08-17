using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    /// <summary>
    /// Deterministic execution-semantics tests over a fake HTTP transport:
    /// the early-stop policy flag as single authority, Android store lookup
    /// privacy gating with one shared cumulative deadline, and deterministic
    /// HttpResponseMessage disposal on every outcome path.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // mutates the process-global SharedHttp client
    public class ExecutionSemanticsTests
    {
        private const string AppleTouchHtml =
            "<html><head><link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\"></head><body></body></html>";
        private const string WeakIconHtml =
            "<html><head><meta property=\"og:image\" content=\"/favicon.png\"></head><body></body></html>";
        private const string PlayPageHtml =
            "<html><head><img src=\"https://play-lh.googleusercontent.com/abc123=s128\"></head><body></body></html>";

        private static byte[] quadrantPng192;
        private static byte[] quadrantPng32;
        private static RoutingHandler currentHandler;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            quadrantPng192 = CreateQuadrantPng(192);
            quadrantPng32 = CreateQuadrantPng(32);
        }

        private static byte[] CreateQuadrantPng(int size)
        {
            using (var bitmap = new Bitmap(size, size))
            {
                for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    Color color;
                    if (x < size / 2 && y < size / 2) color = Color.White;
                    else if (x >= size / 2 && y < size / 2) color = Color.Black;
                    else if (x < size / 2) color = Color.Firebrick;
                    else color = Color.SteelBlue;
                    bitmap.SetPixel(x, y, color);
                }
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            SharedHttp.ResetClientForTests();
            FaviconDownloader.ClearCache();
        }

        private sealed class TrackingResponse : HttpResponseMessage
        {
            public TrackingResponse(HttpStatusCode statusCode, byte[] body)
            {
                StatusCode = statusCode;
                if (body != null)
                    Content = new ByteArrayContent(body);
            }

            public bool Disposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class RoutingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;

            public RoutingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                this.send = send;
                RequestCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Responses = new ConcurrentBag<TrackingResponse>();
            }

            public ConcurrentDictionary<string, int> RequestCounts { get; private set; }
            public ConcurrentBag<TrackingResponse> Responses { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                string host = request.RequestUri != null ? request.RequestUri.Host : string.Empty;
                RequestCounts.AddOrUpdate(host, 1, (key, value) => value + 1);
                return send(request, cancellationToken);
            }
        }

        private static void InstallTransport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            currentHandler = new RoutingHandler(send);
            SharedHttp.ReplaceClientForTests(new HttpClient(currentHandler));
        }

        private static Task<HttpResponseMessage> Tracked(HttpStatusCode code, byte[] body)
        {
            var response = new TrackingResponse(code, body);
            currentHandler.Responses.Add(response);
            return Task.FromResult<HttpResponseMessage>(response);
        }

        private static int RequestsTo(string hostFragment)
        {
            return currentHandler.RequestCounts
                .Where(pair => pair.Key.IndexOf(hostFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                .Sum(pair => pair.Value);
        }

        private static Configuration CustomPolicyConfig(string providerOrderCsv, int primaryMs, int fallbackMs,
            int cumulativeMs, bool stopAfterStrong, long androidStoreOverride = -1)
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace)
            {
                Timeout = 15,
                UseThirdPartyFallbacks = true,
                AllowSyntheticFallbacks = false
            };
            config.FetchProfileId = "custom";

            string[] enabled = providerOrderCsv.Split(',');
            foreach (string providerName in FaviconDownloader.DefaultProviderOrder)
                config.SetProviderEnabled(providerName, enabled.Contains(providerName));
            config.ProviderOrder = providerOrderCsv;

            ace.SetLong("KeeFetch.CustomPrimaryTimeoutMs", primaryMs);
            ace.SetLong("KeeFetch.CustomFallbackTimeoutMs", fallbackMs);
            ace.SetLong("KeeFetch.CustomCumulativeTimeoutMs", cumulativeMs);
            ace.SetLong("KeeFetch.CustomStopAfterStrongResolved", stopAfterStrong ? 1 : 0);
            ace.SetLong("KeeFetch.CustomAllowAndroidStoreLookup", androidStoreOverride);
            return config;
        }

        private static Task<HttpResponseMessage> DirectSiteHostTransport(HttpRequestMessage request,
            string host, string html, string iconPath, byte[] iconBytes)
        {
            string path = request.RequestUri.AbsolutePath;
            if (request.RequestUri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                if (path == "/")
                    return Tracked(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(html));
                if (path == iconPath)
                    return Tracked(HttpStatusCode.OK, iconBytes);
                return Tracked(HttpStatusCode.NotFound, new byte[] { 32 });
            }
            if (request.RequestUri.Host.Equals("www.google.com", StringComparison.OrdinalIgnoreCase))
                return Tracked(HttpStatusCode.OK, quadrantPng192);
            return Tracked(HttpStatusCode.NotFound, new byte[] { 32 });
        }

        [TestMethod]
        public async Task DirectSiteStrongCanonical_StopDisabled_StillQueriesLaterProviders()
        {
            // Direct Site returns a >=0.90 strong canonical nonblank raster; with
            // stopAfterStrongResolved=false the later provider MUST be queried.
            InstallTransport((request, token) =>
                DirectSiteHostTransport(request, "strong-nostop.example", AppleTouchHtml, "/apple-touch-icon.png", quadrantPng192));

            var config = CustomPolicyConfig("Direct Site,Google", 4000, 3500, 12000, false);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://strong-nostop.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.IsTrue(RequestsTo("www.google.com") >= 1,
                "stopAfterStrongResolved=false must query providers after a strong Direct Site result. attempted=["
                + string.Join(";", result.AttemptedProviders.ToArray()) + "]");
            CollectionAssert.Contains(result.AttemptedProviders.ToArray(), "Google");
        }

        [TestMethod]
        public async Task DirectSiteStrongCanonical_StopEnabled_SkipsLaterProviders()
        {
            // Same strong Direct Site result; with stopAfterStrongResolved=true the
            // later provider MUST NOT be queried.
            InstallTransport((request, token) =>
                DirectSiteHostTransport(request, "strong-stop.example", AppleTouchHtml, "/apple-touch-icon.png", quadrantPng192));

            var config = CustomPolicyConfig("Direct Site,Google", 4000, 3500, 12000, true);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://strong-stop.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.AreEqual("Direct Site", result.Provider);
            Assert.AreEqual(0, RequestsTo("www.google.com"),
                "stopAfterStrongResolved=true must stop after a strong Direct Site result. attempted=["
                + string.Join(";", result.AttemptedProviders.ToArray()) + "]");
        }

        [TestMethod]
        public async Task DirectSiteWeakResult_StopEnabled_DoesNotStopEarly()
        {
            // An ordinary weak Direct Site result (og:image backup, 0.52 base
            // confidence + small-image score < 0.72) must not trigger early stop
            // even when early stop is enabled.
            InstallTransport((request, token) =>
                DirectSiteHostTransport(request, "weak-stop.example", WeakIconHtml, "/favicon.png", quadrantPng32));

            var config = CustomPolicyConfig("Direct Site,Google", 4000, 3500, 12000, true);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("https://weak-stop.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.IsTrue(RequestsTo("www.google.com") >= 1,
                "a weak Direct Site result must not stop the chain early. attempted=["
                + string.Join(";", result.AttemptedProviders.ToArray()) + "]");
        }

        private static Task<HttpResponseMessage> AndroidTransport(HttpRequestMessage request,
            CancellationToken token, int domainDelayMs)
        {
            string host = request.RequestUri.Host;
            if (host.Equals("example.com", StringComparison.OrdinalIgnoreCase))
            {
                if (domainDelayMs > 0)
                    return DelayedNotFound(token, domainDelayMs);
                return Tracked(HttpStatusCode.NotFound, new byte[] { 32 });
            }
            if (host.Equals("play.google.com", StringComparison.OrdinalIgnoreCase))
                return Tracked(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(PlayPageHtml));
            if (host.Equals("play-lh.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
                return Tracked(HttpStatusCode.OK, quadrantPng192);
            return Tracked(HttpStatusCode.NotFound, new byte[] { 32 });
        }

        private static async Task<HttpResponseMessage> DelayedNotFound(CancellationToken token, int delayMs)
        {
            await Task.Delay(delayMs, token).ConfigureAwait(false);
            return await Tracked(HttpStatusCode.NotFound, new byte[] { 32 }).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task PrivacyProfile_AndroidRequest_NeverCallsGooglePlay()
        {
            // androidapp:// under the Privacy policy must perform zero calls to
            // play.google.com / googleusercontent.com, even when the store would
            // have returned a valid icon.
            InstallTransport((request, token) => AndroidTransport(request, token, 0));

            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "privacy";
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("androidapp://com.example.app");

            Assert.AreEqual(0, RequestsTo("play.google.com"), "Privacy must not contact the Play store.");
            Assert.AreEqual(0, RequestsTo("googleusercontent.com"), "Privacy must not contact googleusercontent.");
            Assert.IsFalse(result.AttemptedProviders.Contains("Google Play"),
                "Privacy must not even attempt the Google Play path.");
            Assert.IsFalse(result.ProviderMetrics.Any(m => m.ProviderName == "Google Play"),
                "No Google Play metric may be recorded under Privacy.");
        }

        [TestMethod]
        public async Task PermittedPolicy_AndroidRequest_PerformsPlayLookup()
        {
            InstallTransport((request, token) => AndroidTransport(request, token, 0));

            var config = CustomPolicyConfig("Direct Site", 4000, 3500, 12000, false, 1);
            var downloader = new FaviconDownloader(config);

            var result = await downloader.DownloadAsync("androidapp://com.example.app");

            Assert.AreEqual(1, RequestsTo("play.google.com"),
                "A policy that allows the Android store lookup must query the Play store.");
            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.AreEqual("Google Play", result.Provider);
        }

        [TestMethod]
        public async Task AndroidRequest_DomainAndPlayShareOneCumulativeBudget()
        {
            // Domain phase consumes most of the 2500ms cumulative budget; the Play
            // phase must not start with less than the minimum useful slice left.
            InstallTransport((request, token) => AndroidTransport(request, token, 1600));

            var config = CustomPolicyConfig("Direct Site", 2000, 1500, 2500, false, 1);
            var downloader = new FaviconDownloader(config);

            var stopwatch = Stopwatch.StartNew();
            var result = await downloader.DownloadAsync("androidapp://com.example.app");
            stopwatch.Stop();

            Assert.AreEqual(0, RequestsTo("play.google.com"),
                "Play must not start when the cumulative budget has no useful slice left.");
            Assert.IsTrue(result.ProviderMetrics.Any(m =>
                m.ProviderName == "Google Play" && m.Outcome == "skipped-budget-exhausted"),
                "the skipped Play phase must be recorded. metrics=["
                + string.Join(";", result.ProviderMetrics.Select(m => m.ProviderName + ":" + m.Outcome).ToArray()) + "]");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2500 + 1500,
                "the complete Android request must stay within the policy cumulative ceiling (plus test tolerance); took "
                + stopwatch.ElapsedMilliseconds + "ms");
        }

        private void AssertAllResponsesDisposed(string scenario)
        {
            Assert.IsTrue(currentHandler.Responses.Count > 0, scenario + ": expected at least one response.");
            Assert.IsTrue(currentHandler.Responses.All(r => r.Disposed),
                scenario + ": every HttpResponseMessage must be disposed deterministically. "
                + currentHandler.Responses.Count(r => !r.Disposed) + " of "
                + currentHandler.Responses.Count + " were not disposed.");
        }

        [TestMethod]
        public async Task ResolverProvider_DisposesResponseOnSuccess()
        {
            InstallTransport((request, token) => Tracked(HttpStatusCode.OK, quadrantPng192));

            var config = CustomPolicyConfig("Google", 4000, 3500, 12000, false);
            var result = await new FaviconDownloader(config).DownloadAsync("https://dispose-success.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            AssertAllResponsesDisposed("resolver success");
        }

        [TestMethod]
        public async Task ResolverProvider_DisposesResponseOnHttpError()
        {
            InstallTransport((request, token) => Tracked(HttpStatusCode.NotFound, new byte[] { 32 }));

            var config = CustomPolicyConfig("Google", 4000, 3500, 12000, false);
            var result = await new FaviconDownloader(config).DownloadAsync("https://dispose-404.example/");

            Assert.AreEqual(FaviconStatus.NotFound, result.Status);
            AssertAllResponsesDisposed("resolver http error");
        }

        [TestMethod]
        public async Task ResolverProvider_DisposesResponsesAcrossRetry()
        {
            int calls = 0;
            InstallTransport((request, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    return Tracked(HttpStatusCode.ServiceUnavailable, new byte[] { 32 });
                return Tracked(HttpStatusCode.OK, quadrantPng192);
            });

            var config = CustomPolicyConfig("Google", 4000, 3500, 12000, false);
            var result = await new FaviconDownloader(config).DownloadAsync("https://dispose-retry.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.AreEqual(2, currentHandler.Responses.Count, "expected the retry to produce two responses");
            AssertAllResponsesDisposed("resolver retry");
        }

        [TestMethod]
        public async Task ResolverProvider_DisposesResponseOnParsingRejection()
        {
            InstallTransport((request, token) =>
                Tracked(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes("this is not an image")));

            var config = CustomPolicyConfig("Google", 4000, 3500, 12000, false);
            var result = await new FaviconDownloader(config).DownloadAsync("https://dispose-garbage.example/");

            Assert.AreEqual(FaviconStatus.NotFound, result.Status);
            AssertAllResponsesDisposed("resolver parsing rejection");
        }

        [TestMethod]
        public async Task DirectSite_DisposesEveryResponse()
        {
            InstallTransport((request, token) =>
                DirectSiteHostTransport(request, "dispose-direct.example", AppleTouchHtml, "/apple-touch-icon.png", quadrantPng192));

            var config = CustomPolicyConfig("Direct Site", 4000, 3500, 12000, false);
            var result = await new FaviconDownloader(config).DownloadAsync("https://dispose-direct.example/");

            Assert.AreEqual(FaviconStatus.Success, result.Status);
            Assert.IsTrue(currentHandler.Responses.Count >= 2, "expected HTML plus icon responses");
            AssertAllResponsesDisposed("direct site");
        }

        [TestMethod]
        public async Task AndroidMapper_DisposesResponsesOnSuccessAndError()
        {
            InstallTransport((request, token) => AndroidTransport(request, token, 0));

            var candidate = await AndroidAppMapper.FetchGooglePlayIconCandidateAsync("com.example.app", 5000);
            Assert.IsNotNull(candidate);
            AssertAllResponsesDisposed("android mapper success");

            // Non-success store page: response must still be disposed.
            InstallTransport((request, token) => Tracked(HttpStatusCode.NotFound, new byte[] { 32 }));
            var missing = await AndroidAppMapper.FetchGooglePlayIconCandidateAsync("com.example.missing", 5000);
            Assert.IsNull(missing);
            AssertAllResponsesDisposed("android mapper http error");
        }
    }
}
