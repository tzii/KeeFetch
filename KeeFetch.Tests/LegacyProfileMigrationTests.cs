using KeeFetch.FetchProfiles;
using KeePass.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class LegacyProfileMigrationTests
    {
        [DataTestMethod]
        [DataRow("Fast", "bulk-fast")]
        [DataRow("Balanced", "everyday")]
        [DataRow("Thorough", "max-coverage")]
        [DataRow("Custom", "custom")]
        [DataRow("future-value", "custom")]
        public void MapLegacyValue_ReturnsStableProfileId(string legacy, string expected)
        {
            Assert.AreEqual(expected, LegacyProfileMigration.MapLegacyValue(legacy, false));
        }

        [TestMethod]
        public void MapLegacyValue_MissingNewInstallUsesEveryday()
        {
            Assert.AreEqual("everyday", LegacyProfileMigration.MapLegacyValue(null, true));
        }

        [TestMethod]
        public void MapLegacyValue_MissingExistingInstallUsesCustom()
        {
            Assert.AreEqual("custom", LegacyProfileMigration.MapLegacyValue(null, false));
        }

        [TestMethod]
        public void Configuration_FetchProfileId_MigrationIsIdempotent()
        {
            AceCustomConfig ace = new AceCustomConfig();
            ace.SetString("KeeFetch.FetchPresetMode", "Balanced");

            Configuration config1 = new Configuration(ace);
            string id1 = config1.FetchProfileId;
            int version1 = config1.ProfileSchemaVersion;
            string toggles1 = CaptureProviderState(config1);
            string order1 = config1.ProviderOrder;

            // Second read from same AceCustomConfig should be identical
            Configuration config2 = new Configuration(ace);
            string id2 = config2.FetchProfileId;
            int version2 = config2.ProfileSchemaVersion;
            string toggles2 = CaptureProviderState(config2);
            string order2 = config2.ProviderOrder;

            Assert.AreEqual("everyday", id1);
            Assert.AreEqual(id1, id2);
            Assert.AreEqual(1, version1);
            Assert.AreEqual(version1, version2);
            Assert.AreEqual(toggles1, toggles2);
            Assert.AreEqual(order1, order2);

            // Legacy key must remain intact
            string legacyValue = ace.GetString("KeeFetch.FetchPresetMode", null);
            Assert.AreEqual("Balanced", legacyValue);

            // Provider order and toggles not altered by migration
            string storedProfileId = ace.GetString("KeeFetch.FetchProfileId", null);
            Assert.AreEqual("everyday", storedProfileId);
        }

        [TestMethod]
        public void Configuration_UnknownStoredProfileId_ResolvesToCustom()
        {
            AceCustomConfig ace = new AceCustomConfig();
            ace.SetString("KeeFetch.FetchProfileId", "unknown-profile");
            ace.SetLong("KeeFetch.ProfileSchemaVersion", 1);

            Configuration config = new Configuration(ace);
            Assert.AreEqual("custom", config.FetchProfileId);

            // Stored value not rewritten until save — reading again still yields custom in memory
            // but stored still unknown
            string stored = ace.GetString("KeeFetch.FetchProfileId", null);
            Assert.AreEqual("unknown-profile", stored);
        }

        [TestMethod]
        public void Configuration_NewInstall_HasNoLegacyKeyMapsToEveryday()
        {
            AceCustomConfig ace = new AceCustomConfig();
            Configuration config = new Configuration(ace);
            Assert.AreEqual("everyday", config.FetchProfileId);
            Assert.AreEqual(1, config.ProfileSchemaVersion);
        }

        [TestMethod]
        public void Configuration_ProfilePreviewMapsWithoutPersistingMigration()
        {
            AceCustomConfig ace = new AceCustomConfig();
            Configuration config = new Configuration(ace);

            Assert.AreEqual("everyday", config.PreviewFetchProfileId());
            Assert.IsNull(ace.GetString("KeeFetch.FetchProfileId", null));
            Assert.AreEqual(0L, ace.GetLong("KeeFetch.ProfileSchemaVersion", 0));
        }

        private static string CaptureProviderState(Configuration config)
        {
            return string.Join(",",
                config.EnableDirectSiteProvider.ToString(),
                config.EnableTwentyIconsProvider.ToString(),
                config.EnableDuckDuckGoProvider.ToString(),
                config.EnableGoogleProvider.ToString(),
                config.EnableYandexProvider.ToString(),
                config.EnableFaviconeProvider.ToString(),
                config.EnableIconHorseProvider.ToString());
        }
    }
}
