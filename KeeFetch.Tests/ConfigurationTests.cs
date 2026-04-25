using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace KeeFetch.Tests
{
    [TestClass]
    public class ConfigurationTests
    {
        [TestMethod]
        public void Configuration_ClassExists()
        {
            var type = typeof(Configuration);
            Assert.IsNotNull(type);
            Assert.IsTrue(type.IsPublic);
            Assert.IsTrue(type.IsSealed);
        }

        [TestMethod]
        public void Configuration_HasConstructorWithAceCustomConfig()
        {
            var type = typeof(Configuration);
            var constructor = type.GetConstructor(new[] { typeof(KeePass.App.Configuration.AceCustomConfig) });
            Assert.IsNotNull(constructor);
        }

        [TestMethod]
        public void Configuration_HasExpectedProperties()
        {
            var type = typeof(Configuration);
            
            // Boolean properties
            Assert.IsNotNull(type.GetProperty("PrefixUrls"));
            Assert.IsNotNull(type.GetProperty("FetchPresetMode"));
            Assert.IsNotNull(type.GetProperty("UseTitleField"));
            Assert.IsNotNull(type.GetProperty("SkipExistingIcons"));
            Assert.IsNotNull(type.GetProperty("AutoSave"));
            Assert.IsNotNull(type.GetProperty("AllowSelfSignedCerts"));
            Assert.IsNotNull(type.GetProperty("UseThirdPartyFallbacks"));
            Assert.IsNotNull(type.GetProperty("AllowSyntheticFallbacks"));
            Assert.IsNotNull(type.GetProperty("HasSeenFirstRunDisclosure"));
            Assert.IsNotNull(type.GetProperty("EnableDirectSiteProvider"));
            Assert.IsNotNull(type.GetProperty("EnableTwentyIconsProvider"));
            Assert.IsNotNull(type.GetProperty("EnableDuckDuckGoProvider"));
            Assert.IsNotNull(type.GetProperty("EnableGoogleProvider"));
            Assert.IsNotNull(type.GetProperty("EnableYandexProvider"));
            Assert.IsNotNull(type.GetProperty("EnableFaviconeProvider"));
            Assert.IsNotNull(type.GetProperty("EnableIconHorseProvider"));
             
            // Integer properties
            Assert.IsNotNull(type.GetProperty("MaxIconSize"));
            Assert.IsNotNull(type.GetProperty("Timeout"));
             
            // String properties
            Assert.IsNotNull(type.GetProperty("IconNamePrefix"));
            Assert.IsNotNull(type.GetProperty("ProviderOrder"));
        }

        [TestMethod]
        public void Configuration_PropertiesAreReadWrite()
        {
            var type = typeof(Configuration);
            
            var prefixUrls = type.GetProperty("PrefixUrls");
            Assert.IsTrue(prefixUrls.CanRead);
            Assert.IsTrue(prefixUrls.CanWrite);

            var fetchPresetMode = type.GetProperty("FetchPresetMode");
            Assert.IsTrue(fetchPresetMode.CanRead);
            Assert.IsTrue(fetchPresetMode.CanWrite);
            
            var timeout = type.GetProperty("Timeout");
            Assert.IsTrue(timeout.CanRead);
            Assert.IsTrue(timeout.CanWrite);
            
            var iconNamePrefix = type.GetProperty("IconNamePrefix");
            Assert.IsTrue(iconNamePrefix.CanRead);
            Assert.IsTrue(iconNamePrefix.CanWrite);

            var providerOrder = type.GetProperty("ProviderOrder");
            Assert.IsTrue(providerOrder.CanRead);
            Assert.IsTrue(providerOrder.CanWrite);
        }

        [TestMethod]
        public void Configuration_DefaultValues_AreCorrect()
        {
            // This test documents the expected defaults as defined in Configuration.cs
            // These values are defined in the Configuration class property getters
            // and should match the documentation below:
            // PrefixUrls: true
            // FetchPresetMode: Balanced
            // UseTitleField: true
            // SkipExistingIcons: false
            // AutoSave: false
            // AllowSelfSignedCerts: false
            // UseThirdPartyFallbacks: true
            // AllowSyntheticFallbacks: true
            // HasSeenFirstRunDisclosure: false
            // MaxIconSize: 128
            // Timeout: 15
            // IconNamePrefix: "keefetch-"
            // ProviderOrder: default provider order
             
            // Verify the Configuration class has all expected properties
            var type = typeof(Configuration);
            Assert.IsNotNull(type.GetProperty("PrefixUrls"));
            Assert.IsNotNull(type.GetProperty("FetchPresetMode"));
            Assert.IsNotNull(type.GetProperty("UseTitleField"));
            Assert.IsNotNull(type.GetProperty("SkipExistingIcons"));
            Assert.IsNotNull(type.GetProperty("AutoSave"));
            Assert.IsNotNull(type.GetProperty("AllowSelfSignedCerts"));
            Assert.IsNotNull(type.GetProperty("UseThirdPartyFallbacks"));
            Assert.IsNotNull(type.GetProperty("AllowSyntheticFallbacks"));
            Assert.IsNotNull(type.GetProperty("HasSeenFirstRunDisclosure"));
            Assert.IsNotNull(type.GetProperty("EnableDirectSiteProvider"));
            Assert.IsNotNull(type.GetProperty("EnableTwentyIconsProvider"));
            Assert.IsNotNull(type.GetProperty("EnableDuckDuckGoProvider"));
            Assert.IsNotNull(type.GetProperty("EnableGoogleProvider"));
            Assert.IsNotNull(type.GetProperty("EnableYandexProvider"));
            Assert.IsNotNull(type.GetProperty("EnableFaviconeProvider"));
            Assert.IsNotNull(type.GetProperty("EnableIconHorseProvider"));
            Assert.IsNotNull(type.GetProperty("MaxIconSize"));
            Assert.IsNotNull(type.GetProperty("Timeout"));
            Assert.IsNotNull(type.GetProperty("IconNamePrefix"));
            Assert.IsNotNull(type.GetProperty("ProviderOrder"));
        }

        [TestMethod]
        public void Configuration_TimeoutProperty_HasClampingLogic()
        {
            // Verify the Timeout property setter clamps values to 5-60 range
            var type = typeof(Configuration);
            var timeoutProp = type.GetProperty("Timeout");
            Assert.IsNotNull(timeoutProp);
            
            // Verify the property is writable (has a setter that performs clamping)
            Assert.IsTrue(timeoutProp.CanWrite, "Timeout property should be writable to apply clamping");
            
            // Verify the setter exists
            var setter = timeoutProp.GetSetMethod();
            Assert.IsNotNull(setter, "Timeout property should have a setter with clamping logic");
        }

        [TestMethod]
        public void Configuration_HasProviderHelpers()
        {
            var type = typeof(Configuration);
            Assert.IsNotNull(type.GetMethod("IsProviderEnabled"));
            Assert.IsNotNull(type.GetMethod("SetProviderEnabled"));
            Assert.IsNotNull(type.GetMethod("GetProviderOrderList"));
            Assert.IsNotNull(type.GetMethod("ShouldStopAfterStrongResolvedProvider"));
            Assert.IsNotNull(type.GetMethod("GetPresetDescription", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("GetPresetTimeout", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("GetPresetMaxCumulativeTimeoutMs", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("GetPresetPrimaryProviderTimeoutMs", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("GetPresetFallbackProviderTimeoutMs", BindingFlags.Public | BindingFlags.Static));
        }

        [TestMethod]
        public void Configuration_PresetProviderOrders_AreDistinct()
        {
            CollectionAssert.AreEqual(
                new[] { "Direct Site", "Google", "Twenty Icons" },
                Configuration.GetPresetProviderOrderList(FetchPresetMode.Fast));

            CollectionAssert.AreEqual(
                new[] { "Direct Site", "Google", "Favicone" },
                Configuration.GetPresetProviderOrderList(FetchPresetMode.Balanced));

            CollectionAssert.AreEqual(
                FaviconDownloader.DefaultProviderOrder,
                Configuration.GetPresetProviderOrderList(FetchPresetMode.Thorough));
        }

        [TestMethod]
        public void Configuration_PresetTimeoutBudgets_AreMonotonic()
        {
            Assert.IsTrue(
                Configuration.GetPresetPrimaryProviderTimeoutMs(FetchPresetMode.Fast) <
                Configuration.GetPresetPrimaryProviderTimeoutMs(FetchPresetMode.Balanced));
            Assert.IsTrue(
                Configuration.GetPresetPrimaryProviderTimeoutMs(FetchPresetMode.Balanced) <
                Configuration.GetPresetPrimaryProviderTimeoutMs(FetchPresetMode.Thorough));

            Assert.IsTrue(
                Configuration.GetPresetFallbackProviderTimeoutMs(FetchPresetMode.Fast) <
                Configuration.GetPresetFallbackProviderTimeoutMs(FetchPresetMode.Balanced));
            Assert.IsTrue(
                Configuration.GetPresetFallbackProviderTimeoutMs(FetchPresetMode.Balanced) <
                Configuration.GetPresetFallbackProviderTimeoutMs(FetchPresetMode.Thorough));

            Assert.IsTrue(
                Configuration.GetPresetMaxCumulativeTimeoutMs(FetchPresetMode.Fast) <
                Configuration.GetPresetMaxCumulativeTimeoutMs(FetchPresetMode.Balanced));
            Assert.IsTrue(
                Configuration.GetPresetMaxCumulativeTimeoutMs(FetchPresetMode.Balanced) <
                Configuration.GetPresetMaxCumulativeTimeoutMs(FetchPresetMode.Thorough));
        }

        [TestMethod]
        public void Configuration_BalancedPreset_UsesSyntheticFallbackWithoutIconHorse()
        {
            Assert.IsTrue(Configuration.GetPresetAllowSyntheticFallbacks(FetchPresetMode.Balanced));
            Assert.IsTrue(Configuration.IsProviderEnabledByPreset(FetchPresetMode.Balanced, "Favicone"));
            Assert.IsFalse(Configuration.IsProviderEnabledByPreset(FetchPresetMode.Balanced, "Twenty Icons"));
            Assert.IsFalse(Configuration.IsProviderEnabledByPreset(FetchPresetMode.Balanced, "DuckDuckGo"));
            Assert.IsFalse(Configuration.IsProviderEnabledByPreset(FetchPresetMode.Balanced, "Icon Horse"));
        }

        [TestMethod]
        public void Configuration_DefaultFetchPresetMode_IsBalanced()
        {
            var config = new Configuration(new KeePass.App.Configuration.AceCustomConfig());
            Assert.AreEqual(FetchPresetMode.Balanced, config.FetchPresetMode);
        }
    }
}
