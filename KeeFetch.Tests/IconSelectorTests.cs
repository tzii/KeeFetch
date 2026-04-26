using KeeFetch.IconSelection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace KeeFetch.Tests
{
    [TestClass]
    public class IconSelectorTests
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
        public void Select_PrefersTier1OverTier2()
        {
            var selector = new IconSelector();
            var candidates = new List<IconCandidate>
            {
                CreateCandidate("Direct Site", IconTier.SiteCanonical, 0.55, false),
                CreateCandidate("DuckDuckGo", IconTier.StrongResolved, 0.99, false)
            };

            var selection = selector.Select(candidates, new[] { "Direct Site", "DuckDuckGo" }, true);
            Assert.IsNotNull(selection.SelectedCandidate);
            Assert.AreEqual("Direct Site", selection.SelectedCandidate.ProviderName);
        }

        [TestMethod]
        public void Select_PrefersTier2OverTier3()
        {
            var selector = new IconSelector();
            var candidates = new List<IconCandidate>
            {
                CreateCandidate("DuckDuckGo", IconTier.StrongResolved, 0.62, false),
                CreateCandidate("Icon Horse", IconTier.SyntheticFallback, 0.95, true)
            };

            var selection = selector.Select(candidates, new[] { "DuckDuckGo", "Icon Horse" }, true);
            Assert.IsNotNull(selection.SelectedCandidate);
            Assert.AreEqual("DuckDuckGo", selection.SelectedCandidate.ProviderName);
            Assert.IsFalse(selection.WasSyntheticFallback);
        }

        [TestMethod]
        public void Select_RejectsSyntheticWhenStrongExists()
        {
            var selector = new IconSelector();
            var candidates = new List<IconCandidate>
            {
                CreateCandidate("Google", IconTier.StrongResolved, 0.70, false),
                CreateCandidate("Favicone", IconTier.SyntheticFallback, 0.99, true)
            };

            var selection = selector.Select(candidates, new[] { "Google", "Favicone" }, true);
            Assert.IsNotNull(selection.SelectedCandidate);
            Assert.AreEqual("Google", selection.SelectedCandidate.ProviderName);
            Assert.IsTrue(selection.RejectedCandidates.Exists(c => c.ProviderName == "Favicone"));
        }

        [TestMethod]
        public void Select_AllowsSyntheticAsLastResort()
        {
            var selector = new IconSelector();
            var candidates = new List<IconCandidate>
            {
                CreateCandidate("Icon Horse", IconTier.SyntheticFallback, 0.50, true)
            };

            var selection = selector.Select(candidates, new[] { "Icon Horse" }, true);
            Assert.IsNotNull(selection.SelectedCandidate);
            Assert.AreEqual("Icon Horse", selection.SelectedCandidate.ProviderName);
            Assert.IsTrue(selection.WasSyntheticFallback);
        }

        [TestMethod]
        public void Select_RejectsSyntheticWhenDisabled()
        {
            var selector = new IconSelector();
            var candidates = new List<IconCandidate>
            {
                CreateCandidate("Icon Horse", IconTier.SyntheticFallback, 0.50, true)
            };

            var selection = selector.Select(candidates, new[] { "Icon Horse" }, false);
            Assert.IsNull(selection.SelectedCandidate);
            Assert.AreEqual("NotFound", selection.FinalStatus);
            Assert.AreEqual(1, selection.RejectedCandidates.Count);
        }

        private static IconCandidate CreateCandidate(string providerName, IconTier tier,
            double score, bool synthetic)
        {
            return new IconCandidate
            {
                ProviderName = providerName,
                TargetHost = "example.com",
                SourceUrl = "https://example.com/favicon.png",
                Tier = tier,
                RawData = TinyPng,
                NormalizedPngData = TinyPng,
                OriginalFormat = "png",
                Width = 16,
                Height = 16,
                IsSvg = false,
                IsSynthetic = synthetic,
                IsPlaceholderSuspected = synthetic,
                IsBlankSuspected = false,
                ConfidenceScore = score,
                Notes = string.Empty
            };
        }
    }
}
