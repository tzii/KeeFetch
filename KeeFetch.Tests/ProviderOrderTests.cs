using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace KeeFetch.Tests
{
    [TestClass]
    public class ProviderOrderTests
    {
        [TestMethod]
        public void ProviderOrder_TrimsDeduplicatesAndAppendsMissingKnownProviders()
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace);
            config.ProviderOrder = " google,Direct Site,GOOGLE,unknown-provider ";
            CollectionAssert.AreEqual(
                new[] { "Google", "Direct Site", "unknown-provider", "Twenty Icons", "DuckDuckGo", "Yandex", "Favicone", "Icon Horse" },
                config.GetProviderOrderList().ToArray());
        }

        [TestMethod]
        public void ProviderOrder_EmptyReturnsCatalogOrder()
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace);
            config.ProviderOrder = "";
            CollectionAssert.AreEqual(
                new[] { "Direct Site", "Twenty Icons", "DuckDuckGo", "Google", "Yandex", "Favicone", "Icon Horse" },
                config.GetProviderOrderList().ToArray());
        }

        [TestMethod]
        public void ProviderOrder_DuplicateUnknownIsDeduplicatedCaseInsensitively()
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace);
            config.ProviderOrder = "unknown-provider, UNKNOWN-PROVIDER , MyProvider , myprovider ";
            CollectionAssert.AreEqual(
                new[] { "unknown-provider", "MyProvider", "Direct Site", "Twenty Icons", "DuckDuckGo", "Google", "Yandex", "Favicone", "Icon Horse" },
                config.GetProviderOrderList().ToArray());
        }

        [TestMethod]
        public void ProviderOrder_NormalizeHandlesIdsAndTrims()
        {
            var result = KeeFetch.FetchProfiles.FetchProfileCatalog.NormalizeProviderOrder(new[] { "direct-site", "GOOGLE", " yandex " });
            CollectionAssert.AreEqual(
                new[] { "Direct Site", "Google", "Yandex", "Twenty Icons", "DuckDuckGo", "Favicone", "Icon Horse" },
                result.ToArray());
        }
    }
}
