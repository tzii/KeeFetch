using KeeFetch.IconSelection;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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

        [TestMethod]
        public async Task DownloadAsync_ReusesNegativeCacheWhenSameOriginMissesInBatch()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchPresetMode = FetchPresetMode.Custom;
            foreach (string providerName in FaviconDownloader.DefaultProviderOrder)
                config.SetProviderEnabled(providerName, false);

            FaviconDownloader.ClearCache();

            var downloader = new FaviconDownloader(config);
            var first = await downloader.DownloadAsync("https://example.invalid/path");
            var second = await downloader.DownloadAsync("https://example.invalid/other");

            Assert.AreEqual(FaviconStatus.NotFound, first.Status);
            Assert.AreEqual(FaviconStatus.NotFound, second.Status);
            Assert.IsFalse(first.DiagnosticsSummary.Contains("negative-cache-hit"));
            Assert.IsTrue(second.DiagnosticsSummary.Contains("negative-cache-hit"));
            Assert.IsNotNull(second.ProviderMetrics);
            Assert.AreEqual("Cache", second.ProviderMetrics[0].ProviderName);
            Assert.AreEqual("negative-hit", second.ProviderMetrics[0].Outcome);
        }

        [TestMethod]
        public void BuildProviderPipeline_UsesPresetProviderSetOnFreshConfig()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchPresetMode = FetchPresetMode.Balanced;

            var names = GetPipelineProviderNames(config);

            CollectionAssert.AreEqual(
                new[] { "Direct Site", "Google", "Favicone" },
                names);
        }

        [TestMethod]
        public void BuildProviderPipeline_UsesFullProviderSetForThoroughPreset()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchPresetMode = FetchPresetMode.Thorough;

            var names = GetPipelineProviderNames(config);

            CollectionAssert.AreEqual(FaviconDownloader.DefaultProviderOrder, names);
        }

        private static string[] GetPipelineProviderNames(Configuration config)
        {
            var downloader = new FaviconDownloader(config);
            var method = typeof(FaviconDownloader).GetMethod(
                "BuildProviderPipeline",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var providers = (IEnumerable)method.Invoke(downloader, new object[] { false });
            return providers.Cast<object>()
                .Select(p => (string)p.GetType().GetProperty("Name").GetValue(p, null))
                .ToArray();
        }
    }
}
