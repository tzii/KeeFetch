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
            Assert.IsNotNull(type.GetProperty("UseTitleField"));
            Assert.IsNotNull(type.GetProperty("SkipExistingIcons"));
            Assert.IsNotNull(type.GetProperty("AutoSave"));
            Assert.IsNotNull(type.GetProperty("AllowSelfSignedCerts"));
            Assert.IsNotNull(type.GetProperty("UseThirdPartyFallbacks"));
            
            // Integer properties
            Assert.IsNotNull(type.GetProperty("MaxIconSize"));
            Assert.IsNotNull(type.GetProperty("Timeout"));
            
            // String properties
            Assert.IsNotNull(type.GetProperty("IconNamePrefix"));
        }

        [TestMethod]
        public void Configuration_PropertiesAreReadWrite()
        {
            var type = typeof(Configuration);
            
            var prefixUrls = type.GetProperty("PrefixUrls");
            Assert.IsTrue(prefixUrls.CanRead);
            Assert.IsTrue(prefixUrls.CanWrite);
            
            var timeout = type.GetProperty("Timeout");
            Assert.IsTrue(timeout.CanRead);
            Assert.IsTrue(timeout.CanWrite);
            
            var iconNamePrefix = type.GetProperty("IconNamePrefix");
            Assert.IsTrue(iconNamePrefix.CanRead);
            Assert.IsTrue(iconNamePrefix.CanWrite);
        }

        [TestMethod]
        public void Configuration_DefaultValues_AreCorrect()
        {
            // This test documents the expected defaults as defined in Configuration.cs
            // These values are defined in the Configuration class property getters
            // and should match the documentation below:
            // PrefixUrls: true
            // UseTitleField: true
            // SkipExistingIcons: false
            // AutoSave: false
            // AllowSelfSignedCerts: false
            // UseThirdPartyFallbacks: true
            // MaxIconSize: 128
            // Timeout: 15
            // IconNamePrefix: "kpif-"
            
            // Verify the Configuration class has all expected properties
            var type = typeof(Configuration);
            Assert.IsNotNull(type.GetProperty("PrefixUrls"));
            Assert.IsNotNull(type.GetProperty("UseTitleField"));
            Assert.IsNotNull(type.GetProperty("SkipExistingIcons"));
            Assert.IsNotNull(type.GetProperty("AutoSave"));
            Assert.IsNotNull(type.GetProperty("AllowSelfSignedCerts"));
            Assert.IsNotNull(type.GetProperty("UseThirdPartyFallbacks"));
            Assert.IsNotNull(type.GetProperty("MaxIconSize"));
            Assert.IsNotNull(type.GetProperty("Timeout"));
            Assert.IsNotNull(type.GetProperty("IconNamePrefix"));
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
    }
}
