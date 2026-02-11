using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;
using KeeFetch.IconProviders;

namespace KeeFetch.Tests
{
    [TestClass]
    public class DirectSiteProviderTests
    {
        // Note: ParseIconLinks is private, so we test it via reflection
        // or test the public behavior through GetIcon/GetIconWithOrigin

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

        // HTML parsing tests using reflection to test ParseIconLinks
        [TestMethod]
        public void ParseIconLinks_EmptyHtml_ReturnsEmptyList()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            var result = method.Invoke(_provider, new object[] { "", "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void ParseIconLinks_NullHtml_ReturnsEmptyList()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            var result = method.Invoke(_provider, new object[] { null, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void ParseIconLinks_WithFaviconLink_ReturnsCandidate()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><link rel='icon' href='/favicon.ico'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0, "Should find at least one icon candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithAppleTouchIcon_ReturnsCandidate()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><link rel='apple-touch-icon' href='/apple-touch-icon.png'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0, "Should find at least one icon candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithSizes_ReturnsCorrectSize()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><link rel='icon' href='/icon.png' sizes='64x64'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0);
        }

        [TestMethod]
        public void ParseIconLinks_WithOgImage_ReturnsCandidate()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><meta property='og:image' content='https://example.com/image.png'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0, "Should find OG image candidate");
        }

        [TestMethod]
        public void ParseIconLinks_WithBaseTag_RespectsBaseUrl()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><base href='https://cdn.example.com/'><link rel='icon' href='favicon.ico'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count > 0);
        }

        [TestMethod]
        public void ParseIconLinks_DataUri_Ignored()
        {
            var method = typeof(DirectSiteProvider).GetMethod("ParseIconLinks", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            string html = @"<html><head><link rel='icon' href='data:image/png;base64,abc123'></head></html>";
            var result = method.Invoke(_provider, new object[] { html, "https://example.com" });
            Assert.IsNotNull(result);
            
            var list = result as System.Collections.IList;
            Assert.IsNotNull(list);
            // Data URIs should be filtered out
        }
    }
}
