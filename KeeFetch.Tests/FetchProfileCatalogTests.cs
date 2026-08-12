using System;
using System.Linq;
using KeeFetch.FetchProfiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class FetchProfileCatalogTests
    {
        [TestMethod]
        public void Providers_HaveUniqueStableIdsAndPreserveV12DefaultOrder()
        {
            var providers = FetchProfileCatalog.Providers;
            CollectionAssert.AreEqual(
                new[] { "direct-site", "twenty-icons", "duckduckgo", "google", "yandex", "favicone", "icon-horse" },
                providers.Select(p => p.Id).ToArray());
            Assert.AreEqual(providers.Count,
                providers.Select(p => p.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [TestMethod]
        public void FindProvider_AcceptsLegacyDisplayNamesAndIds()
        {
            Assert.AreEqual("direct-site", FetchProfileCatalog.FindProvider("Direct Site").Id);
            Assert.AreEqual("direct-site", FetchProfileCatalog.FindProvider("direct-site").Id);
            Assert.IsNull(FetchProfileCatalog.FindProvider("unknown-provider"));
        }

        [TestMethod]
        public void ManagedProfiles_HaveStableIdsAndValidProviderReferences()
        {
            CollectionAssert.AreEqual(
                new[] { "bulk-fast", "everyday", "privacy", "max-coverage" },
                FetchProfileCatalog.ManagedProfiles.Select(p => p.Id).ToArray());

            foreach (var profile in FetchProfileCatalog.ManagedProfiles)
            {
                Assert.IsTrue(profile.ProviderIds.Count > 0);
                Assert.IsTrue(profile.PrimaryTimeoutMs > 0);
                Assert.IsTrue(profile.FallbackTimeoutMs > 0);
                Assert.IsTrue(profile.CumulativeTimeoutMs >= profile.PrimaryTimeoutMs);
                foreach (string providerId in profile.ProviderIds)
                    Assert.IsNotNull(FetchProfileCatalog.FindProvider(providerId));
            }
        }

        [TestMethod]
        public void PrivacyProfile_UsesNoThirdPartyProviders()
        {
            var profile = FetchProfileCatalog.GetRequiredProfile("privacy");
            Assert.IsTrue(profile.ProviderIds.All(id => !FetchProfileCatalog.FindProvider(id).IsThirdParty));
            Assert.IsFalse(profile.AllowSyntheticFallbacks);
        }
    }
}
