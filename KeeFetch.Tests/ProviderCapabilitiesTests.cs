using KeeFetch.IconProviders;
using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class ProviderCapabilitiesTests
    {
        [TestMethod]
        public void ProviderCapabilities_AssignExpectedTiers()
        {
            Assert.AreEqual(IconTier.SiteCanonical, new DirectSiteProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.StrongResolved, new TwentyIconsProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.StrongResolved, new DuckDuckGoProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.StrongResolved, new GoogleProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.StrongResolved, new YandexProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.SyntheticFallback, new FaviconeProvider().Capabilities.DefaultTier);
            Assert.AreEqual(IconTier.SyntheticFallback, new IconHorseProvider().Capabilities.DefaultTier);
        }

        [TestMethod]
        public void ProviderCapabilities_AssignSyntheticFlags()
        {
            Assert.IsFalse(new DirectSiteProvider().Capabilities.IsSyntheticCapable);
            Assert.IsFalse(new TwentyIconsProvider().Capabilities.IsSyntheticCapable);
            Assert.IsFalse(new DuckDuckGoProvider().Capabilities.IsSyntheticCapable);
            Assert.IsTrue(new FaviconeProvider().Capabilities.IsSyntheticCapable);
            Assert.IsTrue(new IconHorseProvider().Capabilities.IsSyntheticCapable);
        }

        [TestMethod]
        public void ProviderCapabilities_AssignConcurrencyCaps()
        {
            Assert.AreEqual(4, new DirectSiteProvider().Capabilities.ConcurrencyCap);
            Assert.AreEqual(2, new TwentyIconsProvider().Capabilities.ConcurrencyCap);
            Assert.AreEqual(2, new DuckDuckGoProvider().Capabilities.ConcurrencyCap);
            Assert.AreEqual(2, new GoogleProvider().Capabilities.ConcurrencyCap);
            Assert.AreEqual(2, new YandexProvider().Capabilities.ConcurrencyCap);
        }
    }
}
