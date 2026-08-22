using System;
using System.Collections.Generic;
using KeeFetch.FetchProfiles;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    /// <summary>
    /// Exercises the real FaviconDownloader policy resolution path: the managed
    /// catalog, the legacy Custom defaults, and the benchmark override keys must
    /// resolve to exact FetchExecutionPolicy values, and a winning candidate's
    /// policy must be indistinguishable from the managed profile built from it.
    /// </summary>
    [TestClass]
    public class ExecutionPolicyTests
    {
        private static FetchExecutionPolicy Resolve(Configuration config)
        {
            return new FaviconDownloader(config).ResolvedPolicy;
        }

        private static Configuration ManagedConfig(string profileId)
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = profileId;
            return config;
        }

        private static Configuration CustomCandidateConfig(
            string[] providerIds, int primaryMs, int fallbackMs, int cumulativeMs,
            bool allowSynthetic, bool stopAfterStrongResolved, long androidStoreOverride = -1)
        {
            var ace = new AceCustomConfig();
            var config = new Configuration(ace);
            config.FetchProfileId = "custom";

            List<string> displayNames = new List<string>();
            foreach (string id in providerIds)
            {
                ProviderDefinition found = FetchProfileCatalog.FindProvider(id);
                displayNames.Add(found != null ? found.DisplayName : id);
            }

            foreach (string providerName in FaviconDownloader.DefaultProviderOrder)
                config.SetProviderEnabled(providerName, displayNames.Contains(providerName));

            config.ProviderOrder = string.Join(",", displayNames.ToArray());
            config.UseThirdPartyFallbacks = true;
            config.AllowSyntheticFallbacks = allowSynthetic;

            ace.SetLong("KeeFetch.CustomPrimaryTimeoutMs", primaryMs);
            ace.SetLong("KeeFetch.CustomFallbackTimeoutMs", fallbackMs);
            ace.SetLong("KeeFetch.CustomCumulativeTimeoutMs", cumulativeMs);
            ace.SetLong("KeeFetch.CustomStopAfterStrongResolved", stopAfterStrongResolved ? 1 : 0);
            ace.SetLong("KeeFetch.CustomAllowAndroidStoreLookup", androidStoreOverride);
            return config;
        }

        [TestMethod]
        public void BulkFastProfile_ResolvesExactBudgetsChainAndStop()
        {
            var policy = Resolve(ManagedConfig("bulk-fast"));
            Assert.AreEqual(4000, policy.PrimaryTimeoutMs);
            Assert.AreEqual(2500, policy.FallbackTimeoutMs);
            Assert.AreEqual(15000, policy.CumulativeTimeoutMs);
            Assert.IsFalse(policy.AllowSyntheticFallbacks);
            Assert.IsTrue(policy.StopAfterStrongResolved);
            CollectionAssert.AreEqual(
                new[] { "direct-site", "google", "twenty-icons" },
                (System.Collections.ICollection)policy.ProviderIds);
        }

        [TestMethod]
        public void EverydayProfile_ResolvesExactBudgetsChainSyntheticAndFullChain()
        {
            var policy = Resolve(ManagedConfig("everyday"));
            Assert.AreEqual(10000, policy.PrimaryTimeoutMs);
            Assert.AreEqual(5000, policy.FallbackTimeoutMs);
            Assert.AreEqual(45000, policy.CumulativeTimeoutMs);
            Assert.IsTrue(policy.AllowSyntheticFallbacks);
            Assert.IsFalse(policy.StopAfterStrongResolved);
            CollectionAssert.AreEqual(
                new[] { "direct-site", "twenty-icons", "duckduckgo", "google", "yandex", "icon-horse" },
                (System.Collections.ICollection)policy.ProviderIds);
        }

        [TestMethod]
        public void ThoroughProfile_ResolvesExactBudgetsChainAndEarlyStop()
        {
            var policy = Resolve(ManagedConfig("max-coverage"));
            Assert.AreEqual(6000, policy.PrimaryTimeoutMs);
            Assert.AreEqual(3500, policy.FallbackTimeoutMs);
            Assert.AreEqual(22000, policy.CumulativeTimeoutMs);
            Assert.IsFalse(policy.AllowSyntheticFallbacks);
            Assert.IsTrue(policy.StopAfterStrongResolved);
            CollectionAssert.AreEqual(
                new[] { "direct-site", "yandex" },
                (System.Collections.ICollection)policy.ProviderIds);
        }

        [TestMethod]
        public void PrivacyProfile_ResolvesDirectOnlyWithoutSynthetic()
        {
            var policy = Resolve(ManagedConfig("privacy"));
            CollectionAssert.AreEqual(
                new[] { "direct-site" },
                (System.Collections.ICollection)policy.ProviderIds);
            Assert.IsFalse(policy.AllowSyntheticFallbacks);
            Assert.IsTrue(policy.StopAfterStrongResolved);
            Assert.AreEqual(6000, policy.PrimaryTimeoutMs);
            Assert.AreEqual(22000, policy.CumulativeTimeoutMs);
        }

        [TestMethod]
        public void CustomMode_WithOverrideKeys_ResolvesExactPinnedPolicy()
        {
            var policy = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google", "favicone" },
                6000, 3500, 22000, true, true));
            Assert.AreEqual(6000, policy.PrimaryTimeoutMs);
            Assert.AreEqual(3500, policy.FallbackTimeoutMs);
            Assert.AreEqual(22000, policy.CumulativeTimeoutMs);
            Assert.IsTrue(policy.AllowSyntheticFallbacks);
            Assert.IsTrue(policy.StopAfterStrongResolved);
            CollectionAssert.AreEqual(
                new[] { "direct-site", "google", "favicone" },
                (System.Collections.ICollection)policy.ProviderIds);
        }

        [TestMethod]
        public void CustomMode_WithoutOverrides_KeepsLegacyDefaults()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "custom";
            config.UseThirdPartyFallbacks = true;

            var policy = Resolve(config);
            Assert.AreEqual(FetchExecutionPolicy.DefaultPrimaryTimeoutMs, policy.PrimaryTimeoutMs);
            Assert.AreEqual(FetchExecutionPolicy.DefaultFallbackTimeoutMs, policy.FallbackTimeoutMs);
            Assert.AreEqual(FetchExecutionPolicy.DefaultCumulativeTimeoutMs, policy.CumulativeTimeoutMs);
            Assert.IsFalse(policy.StopAfterStrongResolved);
            Assert.AreEqual(7, policy.ProviderIds.Count);
        }

        [TestMethod]
        public void CustomMode_ThirdPartyDisabled_RemovesThirdPartyProviders()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "custom";
            config.UseThirdPartyFallbacks = false;

            var policy = Resolve(config);
            CollectionAssert.AreEqual(
                new[] { "direct-site" },
                (System.Collections.ICollection)policy.ProviderIds);
        }

        [TestMethod]
        public void WinningCandidate_AsManagedProfile_ProducesIdenticalFingerprint()
        {
            // The exact candidate shape used by the v1.3 study for the
            // everyday-style chain; becoming a managed profile must not change
            // the effective execution policy.
            var candidateConfig = CustomCandidateConfig(
                new[] { "direct-site", "google", "favicone" },
                6000, 3500, 22000, true, true);
            string candidateFingerprint = Resolve(candidateConfig).Fingerprint();

            var asManaged = new FetchProfileDefinition(
                "everyday-winner",
                "Balanced",
                "generated",
                "generated",
                new[] { "direct-site", "google", "favicone" },
                6000, 3500, 22000, true, true, true, true,
                "docs/benchmarks/v1.3-provider-study.md");
            string managedFingerprint = FetchExecutionPolicy.FromProfile(asManaged).Fingerprint();

            Assert.AreEqual(managedFingerprint, candidateFingerprint);
        }

        [TestMethod]
        public void Fingerprint_DistinguishesEveryBehaviorField()
        {
            var baseline = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 22000, true, true));

            var differentStop = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 22000, true, false));
            var differentSynthetic = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 22000, false, true));
            var differentOrder = Resolve(CustomCandidateConfig(
                new[] { "google", "direct-site" }, 6000, 3500, 22000, true, true));
            var differentPrimary = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 4000, 3500, 22000, true, true));
            var differentFallback = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 2500, 22000, true, true));
            var differentCumulative = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 15000, true, true));

            Assert.AreNotEqual(baseline.Fingerprint(), differentStop.Fingerprint());
            Assert.AreNotEqual(baseline.Fingerprint(), differentSynthetic.Fingerprint());
            Assert.AreNotEqual(baseline.Fingerprint(), differentOrder.Fingerprint());
            Assert.AreNotEqual(baseline.Fingerprint(), differentPrimary.Fingerprint());
            Assert.AreNotEqual(baseline.Fingerprint(), differentFallback.Fingerprint());
            Assert.AreNotEqual(baseline.Fingerprint(), differentCumulative.Fingerprint());
        }

        [TestMethod]
        public void Fingerprint_IsDeterministicAcrossInstances()
        {
            string a = Resolve(ManagedConfig("everyday")).Fingerprint();
            string b = Resolve(ManagedConfig("everyday")).Fingerprint();
            Assert.AreEqual(a, b);
            Assert.AreEqual(64, a.Length);
        }

        [TestMethod]
        public void PolicyCtor_RejectsTypoProviderId()
        {
            try
            {
                new FetchExecutionPolicy(new[] { "direct-site", "goggle" }, 6000, 3500, 22000, true, true, true);
                Assert.Fail("A typo provider id must fail closed.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void PolicyCtor_RejectsDuplicateProviderId()
        {
            try
            {
                new FetchExecutionPolicy(new[] { "google", "direct-site", "Google" }, 6000, 3500, 22000, true, true, true);
                Assert.Fail("Duplicate provider ids must fail closed.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void PolicyCtor_RejectsEmptyAndBlankChains()
        {
            try
            {
                new FetchExecutionPolicy(new string[0], 6000, 3500, 22000, false, false, false);
                Assert.Fail("An empty provider chain must fail closed.");
            }
            catch (ArgumentException) { }

            try
            {
                new FetchExecutionPolicy(new[] { "  " }, 6000, 3500, 22000, false, false, false);
                Assert.Fail("A blank provider id must fail closed.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void PolicyCtor_RejectsMalformedTimeouts()
        {
            try
            {
                new FetchExecutionPolicy(new[] { "google" }, 0, 3500, 22000, true, true, true);
                Assert.Fail("Zero primary timeout must fail closed.");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                new FetchExecutionPolicy(new[] { "google" }, 6000, -5, 22000, true, true, true);
                Assert.Fail("Negative fallback timeout must fail closed.");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                new FetchExecutionPolicy(new[] { "google" }, 6000, 3500, 0, true, true, true);
                Assert.Fail("Zero cumulative timeout must fail closed.");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                new FetchExecutionPolicy(new[] { "google" }, 6000, 3500, 5000, true, true, true);
                Assert.Fail("Cumulative below primary must fail closed.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void UnknownManagedProfileId_FailsClosed()
        {
            try
            {
                FetchProfileCatalog.GetRequiredProfile("does-not-exist");
                Assert.Fail("Unknown managed profile ids must fail closed.");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void ManagedProfiles_ExposeAndroidStoreLookupPermission()
        {
            Assert.IsFalse(Resolve(ManagedConfig("privacy")).AllowAndroidStoreLookup,
                "Privacy must never perform Google Play lookups.");
            Assert.IsTrue(Resolve(ManagedConfig("bulk-fast")).AllowAndroidStoreLookup);
            Assert.IsTrue(Resolve(ManagedConfig("everyday")).AllowAndroidStoreLookup);
            Assert.IsTrue(Resolve(ManagedConfig("max-coverage")).AllowAndroidStoreLookup);
        }

        [TestMethod]
        public void CustomPolicy_AndroidStoreLookup_DefaultsFromThirdPartyToggle()
        {
            var denied = new Configuration(new AceCustomConfig());
            denied.FetchProfileId = "custom";
            denied.UseThirdPartyFallbacks = false;
            Assert.IsFalse(Resolve(denied).AllowAndroidStoreLookup,
                "With third-party fallbacks disabled, Google Play lookups must be denied by default.");

            var allowed = new Configuration(new AceCustomConfig());
            allowed.FetchProfileId = "custom";
            allowed.UseThirdPartyFallbacks = true;
            Assert.IsTrue(Resolve(allowed).AllowAndroidStoreLookup);
        }

        [TestMethod]
        public void CustomPolicy_AndroidStoreLookup_OverrideWinsAndChangesFingerprint()
        {
            var allow = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 22000, true, true, 1));
            var deny = Resolve(CustomCandidateConfig(
                new[] { "direct-site", "google" }, 6000, 3500, 22000, true, true, 0));

            Assert.IsTrue(allow.AllowAndroidStoreLookup);
            Assert.IsFalse(deny.AllowAndroidStoreLookup);
            Assert.AreNotEqual(allow.Fingerprint(), deny.Fingerprint(),
                "AllowAndroidStoreLookup is behavior-affecting and must change the fingerprint.");
        }
    }
}
