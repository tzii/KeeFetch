using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using KeeFetch.IconProviders;

namespace KeeFetch.Tests
{
    [TestClass]
    public class DirectSiteProviderTests
    {
        // ParseIconLinks is now internal (not private) and accessible via InternalsVisibleTo
        // No need for reflection since AssemblyInfo.cs declares [assembly: InternalsVisibleTo("KeeFetch.Tests")]

        private DirectSiteProvider _provider;

        [TestInitialize]
        public void Setup()
        {
            _provider = new DirectSiteProvider();
        }

        [TestMethod]
        public void Name_ReturnsDirectSite()
        {
            Assert.AreEqual("Direct Site", _provider.Name);
        }

        [TestMethod]
        public void ParseIconLinks_EmptyHtml_ReturnsEmptyList()
        {
            var result = _provider.ParseIconLinks("", "https://example.com");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ParseIconLinks_NullHtml_ReturnsEmptyList()
        {
            var result = _provider.ParseIconLinks(null, "https://example.com");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ParseIconLinks_WithFaviconLink_ReturnsCandidate()
        {
            string html = @"<html><head><link rel='icon' href='/favicon.ico'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find at least one icon candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithAppleTouchIcon_ReturnsCandidate()
        {
            string html = @"<html><head><link rel='apple-touch-icon' href='/apple-touch-icon.png'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find at least one icon candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithSizes_ReturnsCorrectSize()
        {
            string html = @"<html><head><link rel='icon' href='/icon.png' sizes='64x64'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find icon with sizes attribute");
            
            // Verify the size was parsed correctly
            bool foundCorrectSize = false;
            foreach (var candidate in result)
            {
                if (candidate.Size == 64)
                {
                    foundCorrectSize = true;
                    break;
                }
            }
            Assert.IsTrue(foundCorrectSize, "Should parse 64x64 size correctly");
        }

        [TestMethod]
        public void ParseIconLinks_WithOgImage_ReturnsCandidate()
        {
            string html = @"<html><head><meta property='og:image' content='https://example.com/image.png'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find OG image candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithBaseTag_RespectsBaseUrl()
        {
            string html = @"<html><head><base href='https://cdn.example.com/'><link rel='icon' href='favicon.ico'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find icon with base tag");
        }

        [TestMethod]
        public void ParseIconLinks_DataUri_Ignored()
        {
            string html = @"<html><head><link rel='icon' href='data:image/png;base64,abc123'></head></html>";
            var result = _provider.ParseIconLinks(html, "https://example.com");
            Assert.IsNotNull(result);
            // Data URIs should be filtered out - verify no candidates with data: URLs
            foreach (var candidate in result)
            {
                Assert.IsFalse(candidate.Url.StartsWith("data:"), "Data URIs should be filtered out");
            }
        }
    }
}
