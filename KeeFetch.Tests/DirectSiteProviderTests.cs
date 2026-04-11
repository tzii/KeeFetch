using System.Linq;
using KeeFetch.IconProviders;
using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class DirectSiteProviderTests
    {
        private DirectSiteProvider provider;

        [TestInitialize]
        public void Setup()
        {
            provider = new DirectSiteProvider();
        }

        [TestMethod]
        public void Name_ReturnsDirectSite()
        {
            Assert.AreEqual("Direct Site", provider.Name);
        }

        [TestMethod]
        public void ParseIconLinks_EmptyHtml_ReturnsEmptyList()
        {
            var result = provider.ParseIconLinks(string.Empty, "https://example.com");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ParseIconLinks_WithFaviconLink_ReturnsCandidate()
        {
            string html = @"<html><head><link rel='icon' href='/favicon.ico'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            Assert.IsTrue(result.Any(), "Should find at least one icon candidate");
            Assert.IsTrue(result.Any(c => c.Url == "https://example.com/favicon.ico"));
            Assert.IsTrue(result.All(c => c.Tier == IconTier.SiteCanonical || c.Tier == IconTier.StrongResolved));
        }

        [TestMethod]
        public void ParseIconLinks_WithAppleTouchIcon_ReturnsHighPriorityCandidate()
        {
            string html = @"<html><head><link rel='apple-touch-icon' href='/apple-touch-icon.png'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            Assert.IsTrue(result.Any(), "Should find at least one icon candidate");
            var apple = result.FirstOrDefault(c => c.SourceType == "apple-touch-icon");
            Assert.IsNotNull(apple, "apple-touch candidate should be present");
            Assert.AreEqual(1, apple.Priority);
            Assert.AreEqual(IconTier.SiteCanonical, apple.Tier);
        }

        [TestMethod]
        public void ParseIconLinks_WithSizes_ReturnsCorrectSize()
        {
            string html = @"<html><head><link rel='icon' href='/icon.png' sizes='64x64'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            Assert.IsTrue(result.Any(c => c.Size == 64), "Should parse 64x64 size correctly");
        }

        [TestMethod]
        public void ParseIconLinks_WithOgImage_UsesLowerTierBackup()
        {
            string html = @"<html><head><meta property='og:image' content='https://example.com/image.png'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            var og = result.FirstOrDefault(c => c.SourceType == "og:image-backup");
            Assert.IsNotNull(og, "Should parse og:image");
            Assert.AreEqual(IconTier.StrongResolved, og.Tier);
            Assert.IsTrue(og.Priority >= 10, "og:image should be deprioritized versus site-canonical icons");
        }

        [TestMethod]
        public void ParseIconLinks_WithBaseTag_RespectsSameHostBaseUrl()
        {
            string html = @"<html><head><base href='https://example.com/assets/'><link rel='icon' href='favicon.ico'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            Assert.IsTrue(result.Any(c => c.Url == "https://example.com/assets/favicon.ico"),
                "Should resolve icon URL relative to <base> href");
        }

        [TestMethod]
        public void ParseIconLinks_DataUri_Ignored()
        {
            string html = @"<html><head><link rel='icon' href='data:image/png;base64,abc123'></head></html>";
            var result = provider.ParseIconLinks(html, "https://example.com");

            Assert.IsFalse(result.Any(c => c.Url.StartsWith("data:")), "Data URIs should be filtered out");
        }

        [TestMethod]
        public void ParseManifestLinks_FindsManifestHref()
        {
            string html = @"<html><head><link rel='manifest' href='/site.webmanifest'></head></html>";
            var links = provider.ParseManifestLinks(html, "https://example.com");

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual("https://example.com/site.webmanifest", links[0]);
        }

        [TestMethod]
        public void ParseManifestIcons_ResolvesRelativeUrlsAndDetectsSvg()
        {
            string manifest = @"{
  ""icons"": [
    { ""src"": ""/icons/icon-192.png"", ""sizes"": ""192x192"", ""type"": ""image/png"" },
    { ""src"": ""/icons/icon.svg"", ""sizes"": ""any"", ""type"": ""image/svg+xml"" }
  ]
}";

            var icons = provider.ParseManifestIcons(manifest, "https://example.com/site.webmanifest");

            Assert.IsTrue(icons.Any(i => i.Url == "https://example.com/icons/icon-192.png"));
            Assert.IsTrue(icons.Any(i => i.Url == "https://example.com/icons/icon.svg" && i.IsSvgHint));
            Assert.IsTrue(icons.All(i => i.Tier == IconTier.SiteCanonical));
        }
    }
}
