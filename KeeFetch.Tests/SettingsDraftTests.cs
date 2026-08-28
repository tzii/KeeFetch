using System;
using System.Linq;
using KeeFetch.FetchProfiles;
using KeeFetch.Settings;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class SettingsDraftTests
    {
        [TestMethod]
        public void Draft_DoesNotMutateConfigurationUntilApply()
        {
            var rawConfig = new AceCustomConfig();
            var config = new Configuration(rawConfig);
            SettingsDraft draft = SettingsDraft.FromConfiguration(config);

            draft.ProfileId = "privacy";
            draft.AutoSave = true;

            Assert.IsNull(rawConfig.GetString("KeeFetch.FetchProfileId", null),
                "Opening Settings must not persist a default profile before Save.");
            Assert.AreEqual(0L,
                rawConfig.GetLong("KeeFetch.ProfileSchemaVersion", 0),
                "Opening Settings must not persist a schema version before Save.");
            Assert.AreEqual("everyday", config.FetchProfileId);
            Assert.IsFalse(config.AutoSave);

            draft.ApplyTo(config);

            Assert.AreEqual("privacy", config.FetchProfileId);
            Assert.IsTrue(config.AutoSave);
            Assert.IsFalse(config.UseThirdPartyFallbacks);
            CollectionAssert.AreEqual(new[] { "Direct Site" }, config.GetProviderOrderList().Take(1).ToArray());
        }

        [TestMethod]
        public void FromConfiguration_CopiesCustomSettingsWithoutSharingProviderState()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "custom";
            config.PrefixUrls = false;
            config.UseTitleField = false;
            config.SkipExistingIcons = true;
            config.AutoSave = true;
            config.AllowSelfSignedCerts = true;
            config.UseThirdPartyFallbacks = false;
            config.AllowSyntheticFallbacks = false;
            config.HasSeenFirstRunDisclosure = true;
            config.MaxIconSize = 64;
            config.Timeout = 27;
            config.IconNamePrefix = "custom-";
            config.ProviderOrder = "Google,Direct Site";

            foreach (string provider in config.GetProviderOrderList())
                config.SetProviderEnabled(provider, false);
            config.SetProviderEnabled("Google", true);
            config.SetProviderEnabled("Direct Site", true);

            SettingsDraft draft = SettingsDraft.FromConfiguration(config);

            Assert.AreEqual("custom", draft.ProfileId);
            Assert.IsFalse(draft.PrefixUrls);
            Assert.IsFalse(draft.UseTitleField);
            Assert.IsTrue(draft.SkipExistingIcons);
            Assert.IsTrue(draft.AutoSave);
            Assert.IsTrue(draft.AllowSelfSignedCerts);
            Assert.IsFalse(draft.UseThirdPartyFallbacks);
            Assert.IsFalse(draft.AllowSyntheticFallbacks);
            Assert.IsTrue(draft.HasSeenFirstRunDisclosure);
            Assert.AreEqual(64, draft.MaxIconSize);
            Assert.AreEqual(27, draft.Timeout);
            Assert.AreEqual("custom-", draft.IconNamePrefix);
            Assert.AreEqual("google", draft.ProviderOrder[0]);
            Assert.AreEqual("direct-site", draft.ProviderOrder[1]);
            Assert.IsTrue(draft.IsProviderEnabled("google"));
            Assert.IsTrue(draft.IsProviderEnabled("direct-site"));
            Assert.IsFalse(draft.IsProviderEnabled("yandex"));

            draft.ProviderOrder.RemoveAt(0);
            draft.SetProviderEnabled("google", false);

            Assert.AreEqual("Google", config.GetProviderOrderList()[0]);
            Assert.IsTrue(config.IsProviderEnabled("Google"));
        }

        [TestMethod]
        public void Validate_RejectsUnknownProfile()
        {
            SettingsDraft draft = SettingsDraft.FromConfiguration(
                new Configuration(new AceCustomConfig()));
            draft.ProfileId = "future-profile";

            AssertHasError(draft, "overview", "profile");
        }

        [TestMethod]
        public void Validate_RejectsTimeoutOutsideSupportedRange()
        {
            SettingsDraft draft = CustomDraft();
            draft.Timeout = 4;
            AssertHasError(draft, "advanced", "timeout");

            draft.Timeout = 61;
            AssertHasError(draft, "advanced", "timeout");
        }

        [TestMethod]
        public void Validate_RejectsIconSizeOutsideSupportedRange()
        {
            SettingsDraft draft = CustomDraft();
            draft.MaxIconSize = 15;
            AssertHasError(draft, "downloads", "max-icon-size");

            draft.MaxIconSize = 257;
            AssertHasError(draft, "downloads", "max-icon-size");
        }

        [TestMethod]
        public void Validate_RejectsDuplicateProviderIdsCaseInsensitively()
        {
            SettingsDraft draft = CustomDraft();
            draft.ProviderOrder.Clear();
            draft.ProviderOrder.Add("direct-site");
            draft.ProviderOrder.Add("DIRECT-SITE");

            AssertHasError(draft, "providers", "provider-order");
        }

        [TestMethod]
        public void Validate_RejectsThirdPartyOverridesForPrivacyProfile()
        {
            SettingsDraft draft = SettingsDraft.FromConfiguration(
                new Configuration(new AceCustomConfig()));
            draft.ProfileId = "privacy";
            draft.UseThirdPartyFallbacks = true;
            draft.SetProviderEnabled("google", true);

            AssertHasError(draft, "providers", "third-party-providers");
        }

        [TestMethod]
        public void ApplyTo_InvalidDraftDoesNotPartiallyMutateConfiguration()
        {
            var config = new Configuration(new AceCustomConfig());
            SettingsDraft draft = SettingsDraft.FromConfiguration(config);
            draft.AutoSave = true;
            draft.Timeout = 4;

            bool threw = false;
            try
            {
                draft.ApplyTo(config);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Invalid settings should reject ApplyTo.");
            Assert.IsFalse(config.AutoSave, "No setting should be written when validation fails.");
            Assert.AreEqual(15, config.Timeout);
        }

        [TestMethod]
        public void ApplyTo_CommitsAllCustomSettingsAndProviderState()
        {
            var config = new Configuration(new AceCustomConfig());
            SettingsDraft draft = SettingsDraft.FromConfiguration(config);
            draft.ProfileId = "custom";
            draft.PrefixUrls = false;
            draft.UseTitleField = false;
            draft.SkipExistingIcons = true;
            draft.AutoSave = true;
            draft.AllowSelfSignedCerts = true;
            draft.UseThirdPartyFallbacks = false;
            draft.AllowSyntheticFallbacks = false;
            draft.HasSeenFirstRunDisclosure = true;
            draft.MaxIconSize = 96;
            draft.Timeout = 31;
            draft.IconNamePrefix = "saved-";
            draft.ProviderOrder.Clear();
            draft.ProviderOrder.Add("google");
            draft.ProviderOrder.Add("direct-site");
            foreach (string providerId in new[]
            {
                "direct-site", "twenty-icons", "duckduckgo", "google",
                "yandex", "favicone", "icon-horse"
            })
            {
                draft.SetProviderEnabled(providerId, false);
            }
            draft.SetProviderEnabled("google", true);
            draft.SetProviderEnabled("direct-site", true);

            draft.ApplyTo(config);

            Assert.AreEqual("custom", config.FetchProfileId);
            Assert.IsFalse(config.PrefixUrls);
            Assert.IsFalse(config.UseTitleField);
            Assert.IsTrue(config.SkipExistingIcons);
            Assert.IsTrue(config.AutoSave);
            Assert.IsTrue(config.AllowSelfSignedCerts);
            Assert.IsTrue(config.UseThirdPartyFallbacks,
                "Custom must enable its effective third-party chain when Google is checked.");
            Assert.IsFalse(config.AllowSyntheticFallbacks);
            Assert.IsTrue(config.HasSeenFirstRunDisclosure);
            Assert.AreEqual(96, config.MaxIconSize);
            Assert.AreEqual(31, config.Timeout);
            Assert.AreEqual("saved-", config.IconNamePrefix);
            Assert.AreEqual("Google", config.GetProviderOrderList()[0]);
            Assert.AreEqual("Direct Site", config.GetProviderOrderList()[1]);
            Assert.IsTrue(config.IsProviderEnabled("Google"));
            Assert.IsTrue(config.IsProviderEnabled("Direct Site"));
            Assert.IsFalse(config.IsProviderEnabled("Yandex"));
            CollectionAssert.AreEqual(
                new[] { "google", "direct-site" },
                (System.Collections.ICollection)new FaviconDownloader(config)
                    .ResolvedPolicy.ProviderIds);
        }

        [TestMethod]
        public void ApplyTo_PrivacyThenCustomDerivesThirdPartyGateFromCheckedProviders()
        {
            var config = new Configuration(new AceCustomConfig());
            SettingsDraft draft = SettingsDraft.FromConfiguration(config);
            draft.ProfileId = "privacy";
            Assert.IsFalse(draft.UseThirdPartyFallbacks);

            draft.ProfileId = "custom";
            draft.ProviderOrder.Add("google");
            draft.SetProviderEnabled("google", true);
            draft.ApplyTo(config);

            Assert.IsTrue(config.UseThirdPartyFallbacks);
            CollectionAssert.Contains(
                (System.Collections.ICollection)new FaviconDownloader(config)
                    .ResolvedPolicy.ProviderIds,
                "google",
                "A checked third-party provider must be present in the effective Custom chain.");
        }

        [TestMethod]
        public void ApplyTo_LegacyCustomDerivesThirdPartyGateFromCheckedProviders()
        {
            var config = new Configuration(new AceCustomConfig());
            config.FetchProfileId = "custom";
            config.UseThirdPartyFallbacks = false;
            config.ProviderOrder = "Google,Direct Site";
            config.SetProviderEnabled("Google", true);
            config.SetProviderEnabled("Direct Site", true);

            SettingsDraft draft = SettingsDraft.FromConfiguration(config);
            draft.ApplyTo(config);

            Assert.IsTrue(config.UseThirdPartyFallbacks);
            CollectionAssert.Contains(
                (System.Collections.ICollection)new FaviconDownloader(config)
                    .ResolvedPolicy.ProviderIds,
                "google",
                "A checked legacy Custom provider must remain in the effective chain.");
        }

        private static SettingsDraft CustomDraft()
        {
            SettingsDraft draft = SettingsDraft.FromConfiguration(
                new Configuration(new AceCustomConfig()));
            draft.ProfileId = "custom";
            return draft;
        }

        private static void AssertHasError(SettingsDraft draft, string pageId, string controlKey)
        {
            SettingsValidationError error = draft.Validate().FirstOrDefault(
                item => item.PageId == pageId && item.ControlKey == controlKey);
            Assert.IsNotNull(error,
                "Expected validation error for " + pageId + "/" + controlKey + ".");
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.Message));
        }
    }
}
