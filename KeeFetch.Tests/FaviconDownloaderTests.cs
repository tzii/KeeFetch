using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class FaviconDownloaderTests
    {
        private static readonly byte[] TinyPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
            0x01, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        };

        [TestMethod]
        public void CacheIcon_PreservesProviderTierAndSyntheticMetadata()
        {
            const string cacheKey = "https://example.com:443";

            FaviconDownloader.ClearCache();
            FaviconDownloader.CacheIcon(cacheKey, TinyPng, "Icon Horse",
                IconTier.SyntheticFallback, true, "selected=Icon Horse");

            var cached = FaviconDownloader.GetCachedEntry(cacheKey);
            Assert.IsNotNull(cached);
            Assert.AreEqual("Icon Horse", cached.Provider);
            Assert.AreEqual(IconTier.SyntheticFallback, cached.SelectedTier);
            Assert.IsTrue(cached.WasSyntheticFallback);
            Assert.AreEqual("selected=Icon Horse", cached.DiagnosticsSummary);
            CollectionAssert.AreEqual(TinyPng, cached.IconData);
        }
    }
}
