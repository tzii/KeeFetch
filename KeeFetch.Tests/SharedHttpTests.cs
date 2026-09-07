using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using KeeFetch.IconProviders;
using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    /// <summary>
    /// Certificate policy must stay scoped to KeeFetch's own client and never leak
    /// into the process-wide ServicePointManager state shared with KeePass.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // mutates the process-global SharedHttp client and policy counters
    public class SharedHttpTests
    {
        // Generated at runtime rather than hard-coded so the fixture is valid on
        // every System.Drawing backend the suite may run on.
        private static readonly byte[] TinyPng = CreatePng(16);

        private static byte[] CreatePng(int size)
        {
            using (var bitmap = new Bitmap(size, size))
            {
                for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    bitmap.SetPixel(x, y, (x + y) % 2 == 0 ? Color.Black : Color.White);
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
            while (SharedHttp.AllowSelfSignedCertificates)
                SharedHttp.SetAllowSelfSignedCertificates(false);
            SharedHttp.ResetClientForTests();
        }

        [TestMethod]
        public void ValidateServerCertificate_NoErrors_AlwaysAccepted()
        {
            Assert.IsTrue(SharedHttp.ValidateServerCertificate(null, null, null, SslPolicyErrors.None));
        }

        [TestMethod]
        public void ValidateServerCertificate_ChainErrors_RejectedByDefault()
        {
            Assert.IsFalse(SharedHttp.AllowSelfSignedCertificates);
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(null, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [TestMethod]
        public void ValidateServerCertificate_ChainErrors_AcceptedWhileScopeActive()
        {
            SharedHttp.SetAllowSelfSignedCertificates(true);
            Assert.IsTrue(SharedHttp.ValidateServerCertificate(null, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [TestMethod]
        public void ValidateServerCertificate_NameMismatch_RejectedEvenWhenAllowed()
        {
            SharedHttp.SetAllowSelfSignedCertificates(true);
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(null, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch));
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(null, null, null,
                SslPolicyErrors.RemoteCertificateNameMismatch));
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(null, null, null,
                SslPolicyErrors.RemoteCertificateNotAvailable));
        }

        [TestMethod]
        public void SetAllowSelfSignedCertificates_IsReferenceCounted()
        {
            SharedHttp.SetAllowSelfSignedCertificates(true);
            SharedHttp.SetAllowSelfSignedCertificates(true);
            SharedHttp.SetAllowSelfSignedCertificates(false);
            Assert.IsTrue(SharedHttp.AllowSelfSignedCertificates, "one scope should still be active");

            SharedHttp.SetAllowSelfSignedCertificates(false);
            Assert.IsFalse(SharedHttp.AllowSelfSignedCertificates);

            // Unbalanced release must not underflow into a permanently-off state.
            SharedHttp.SetAllowSelfSignedCertificates(false);
            SharedHttp.SetAllowSelfSignedCertificates(true);
            Assert.IsTrue(SharedHttp.AllowSelfSignedCertificates);
        }

        [TestMethod]
        public void SharedClient_PinsTls12AndTls13OnItsOwnHandler()
        {
            SharedHttp.ResetClientForTests();
            FieldInfo handlerField = typeof(HttpMessageInvoker).GetField("_handler",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handlerField, "HttpMessageInvoker handler field not found");
            var clientHandler = handlerField.GetValue(SharedHttp.Instance) as HttpClientHandler;
            Assert.IsNotNull(clientHandler, "shared client must be backed by HttpClientHandler");
            Assert.AreEqual(SharedHttp.RequiredSslProtocols, clientHandler.SslProtocols);
            Assert.IsTrue((clientHandler.SslProtocols & SslProtocols.Tls) == 0, "TLS 1.0 must be excluded");
            Assert.IsTrue((clientHandler.SslProtocols & SslProtocols.Tls11) == 0, "TLS 1.1 must be excluded");
        }

        [TestMethod]
        public void SetupSelfSignedCerts_DoesNotTouchProcessWideCallback()
        {
            RemoteCertificateValidationCallback before = ServicePointManager.ServerCertificateValidationCallback;
            SecurityProtocolType protocolBefore = ServicePointManager.SecurityProtocol;

            FaviconDownloader.SetupTls();
            FaviconDownloader.SetupSelfSignedCerts(true);
            try
            {
                Assert.AreSame(before, ServicePointManager.ServerCertificateValidationCallback);
                Assert.AreEqual(protocolBefore, ServicePointManager.SecurityProtocol);
            }
            finally
            {
                FaviconDownloader.SetupSelfSignedCerts(false);
            }

            Assert.AreSame(before, ServicePointManager.ServerCertificateValidationCallback);
        }

        [TestMethod]
        public async Task ResolverProvider_AbsoluteInternalNames_NeverReachTransport()
        {
            var handler = new RedirectingHandler(new Uri("https://cdn.example.net/icon.png"), TinyPng);
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            foreach (string url in new[]
            {
                "https://localhost./", "https://SERVER.INTERNAL./", "https://printer./"
            })
            {
                var request = new IconRequest
                {
                    TargetHost = new Uri(url).Host,
                    MaxIconSize = 64,
                    TimeoutMs = 5000
                };
                var candidates = await new GoogleProvider().GetCandidatesAsync(request, CancellationToken.None);
                Assert.AreEqual(0, candidates.Count, url);
            }
            Assert.AreEqual(0, handler.RequestCount, "Internal names must not be disclosed to a resolver.");
        }

        [TestMethod]
        public async Task ResolverProvider_RejectsRedirectToPrivateHost()
        {
            // Simulates the transport having followed a redirect: the response's
            // RequestMessage points at the final (private) hop.
            var handler = new RedirectingHandler(new Uri("http://169.254.169.254/latest/meta-data"), TinyPng);
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            var provider = new GoogleProvider();
            var request = new IconRequest
            {
                TargetHost = "example.com",
                MaxIconSize = 64,
                TimeoutMs = 5000
            };

            IReadOnlyList<IconCandidate> candidates = await provider.GetCandidatesAsync(request, CancellationToken.None);
            Assert.AreEqual(0, candidates.Count, "a redirect into a private range must yield no candidate");
        }

        [TestMethod]
        public async Task ResolverProvider_AcceptsRedirectToPublicHost()
        {
            var handler = new RedirectingHandler(new Uri("https://cdn.example.net/icon.png"), TinyPng);
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            var provider = new GoogleProvider();
            var request = new IconRequest
            {
                TargetHost = "example.com",
                MaxIconSize = 64,
                TimeoutMs = 5000
            };

            IReadOnlyList<IconCandidate> candidates = await provider.GetCandidatesAsync(request, CancellationToken.None);
            Assert.AreEqual(1, candidates.Count);
        }

        [TestMethod]
        public void ValidateServerCertificate_ChainErrors_PublicHostRejectedEvenWhenAllowed()
        {
            SharedHttp.SetAllowSelfSignedCertificates(true);
            var publicRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://www.google.com/s2/favicons"));
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(publicRequest, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors),
                "the override must never disable validation for public hosts");
        }

        [TestMethod]
        public void ValidateServerCertificate_ChainErrors_PrivateHostAcceptedWhenAllowed()
        {
            SharedHttp.SetAllowSelfSignedCertificates(true);
            var privateRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://router.local/icon"));
            Assert.IsTrue(SharedHttp.ValidateServerCertificate(privateRequest, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors));
            Assert.IsFalse(SharedHttp.ValidateServerCertificate(privateRequest, null, null,
                SslPolicyErrors.RemoteCertificateNameMismatch));
        }

        [TestMethod]
        public async Task ResolverProvider_NeverContactsPrivateRedirectTarget()
        {
            var handler = new ScriptedHandler(delegate (Uri hop)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("http://169.254.169.254/latest/meta-data") }
                };
                return redirect;
            });
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));
            var request = new IconRequest
            {
                TargetHost = "example.com",
                MaxIconSize = 64,
                TimeoutMs = 5000
            };

            IReadOnlyList<IconCandidate> candidates = await new GoogleProvider().GetCandidatesAsync(request, CancellationToken.None);
            Assert.AreEqual(0, candidates.Count, "a redirect into a private range must yield no candidate");
            Assert.AreEqual(1, handler.RequestedUris.Count, "only the resolver request may be issued");
            Assert.AreNotEqual("169.254.169.254", handler.RequestedUris[0].Host,
                "the private redirect target must never be contacted");
        }

        [TestMethod]
        public async Task ResolverProvider_FollowsPublicRedirectChain()
        {
            var handler = new ScriptedHandler(delegate (Uri hop)
            {
                if (hop.Host == "cdn.example.net")
                {
                    var ok = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(TinyPng)
                    };
                    ok.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    return ok;
                }
                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://cdn.example.net/icon.png") }
                };
            });
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            var request = new IconRequest
            {
                TargetHost = "example.com",
                MaxIconSize = 64,
                TimeoutMs = 5000
            };

            IReadOnlyList<IconCandidate> candidates = await new GoogleProvider().GetCandidatesAsync(request, CancellationToken.None);
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(2, handler.RequestedUris.Count, "the public redirect must be followed exactly once");
            Assert.AreEqual("cdn.example.net", handler.RequestedUris[1].Host);
        }

        [TestMethod]
        public async Task ResolverProvider_StopsAtManualRedirectLimit()
        {
            int hops = 0;
            var handler = new ScriptedHandler(delegate (Uri hop)
            {
                hops++;
                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://hop" + hops + ".example.net/icon.png") }
                };
            });
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            var request = new IconRequest
            {
                TargetHost = "example.com",
                MaxIconSize = 64,
                TimeoutMs = 5000
            };

            IReadOnlyList<IconCandidate> candidates = await new GoogleProvider().GetCandidatesAsync(request, CancellationToken.None);
            Assert.AreEqual(0, candidates.Count);
            Assert.AreEqual(SharedHttp.MaxManualRedirects + 1, handler.RequestedUris.Count,
                "the redirect budget must cap the number of hops");
        }

        [TestMethod]
        public async Task SendFollowingRedirects_PrivateHopAllowedWhenPolicyPermits()
        {
            var handler = new ScriptedHandler(delegate (Uri hop)
            {
                if (hop.Host == "router.local")
                {
                    var ok = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(TinyPng)
                    };
                    return ok;
                }
                return new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("https://router.local/icon.png") }
                };
            });
            SharedHttp.ReplaceClientForTests(new HttpClient(handler));

            HttpResponseMessage response = await SharedHttp.SendFollowingRedirectsAsync(
                "https://router.example.net/", null, delegate { return true; },
                "test", CancellationToken.None);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, handler.RequestedUris.Count);
            response.Dispose();
        }

        private sealed class RedirectingHandler : HttpMessageHandler
        {
            private readonly Uri finalUri;
            private readonly byte[] body;

            public int RequestCount { get; private set; }

            public RedirectingHandler(Uri finalUri, byte[] body)
            {
                this.finalUri = finalUri;
                this.body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(body),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return Task.FromResult(response);
            }
        }

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<Uri, HttpResponseMessage> respond;

            public ScriptedHandler(Func<Uri, HttpResponseMessage> respond)
            {
                this.respond = respond;
            }

            public IList<Uri> RequestedUris { get; } = new List<Uri>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestedUris.Add(request.RequestUri);
                HttpResponseMessage response = respond(request.RequestUri);
                response.RequestMessage = request;
                return Task.FromResult(response);
            }
        }
    }
}
