using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class AndroidAppMapperTests
    {
        [TestMethod]
        public void IsAndroidUrl_AndroidAppUrl_ReturnsTrue()
        {
            Assert.IsTrue(AndroidAppMapper.IsAndroidUrl("androidapp://com.example.app"));
        }

        [TestMethod]
        public void IsAndroidUrl_HttpsUrl_ReturnsFalse()
        {
            Assert.IsFalse(AndroidAppMapper.IsAndroidUrl("https://example.com"));
        }

        [TestMethod]
        public void IsAndroidUrl_Null_ReturnsFalse()
        {
            Assert.IsFalse(AndroidAppMapper.IsAndroidUrl(null));
        }

        [TestMethod]
        public void IsAndroidUrl_Empty_ReturnsFalse()
        {
            Assert.IsFalse(AndroidAppMapper.IsAndroidUrl(""));
        }

        [TestMethod]
        public void GetPackageName_ValidUrl_ReturnsPackage()
        {
            string result = AndroidAppMapper.GetPackageName("androidapp://com.example.app");
            Assert.AreEqual("com.example.app", result);
        }

        [TestMethod]
        public void GetPackageName_WithPath_ReturnsPackage()
        {
            string result = AndroidAppMapper.GetPackageName("androidapp://com.example.app/activity");
            Assert.AreEqual("com.example.app", result);
        }

        [TestMethod]
        public void GetPackageName_InvalidUrl_ReturnsNull()
        {
            string result = AndroidAppMapper.GetPackageName("https://example.com");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void MapToWebDomain_KnownMapping_ReturnsDomain()
        {
            string result = AndroidAppMapper.MapToWebDomain("androidapp://com.google.android.gm");
            Assert.AreEqual("gmail.com", result);
        }

        [TestMethod]
        public void MapToWebDomain_UnknownPackage_GuessesDomain()
        {
            string result = AndroidAppMapper.MapToWebDomain("androidapp://com.example.app");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void MapToWebDomain_InvalidUrl_ReturnsNull()
        {
            string result = AndroidAppMapper.MapToWebDomain("not an android url");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryGuessFromPackage_ComPackage_ReturnsReversed()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("com.example.app");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void TryGuessFromPackage_OrgPackage_ReturnsReversed()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("org.example.app");
            Assert.AreEqual("example.org", result);
        }

        [TestMethod]
        public void TryGuessFromPackage_NetPackage_ReturnsReversed()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("net.example.app");
            Assert.AreEqual("example.net", result);
        }

        [TestMethod]
        public void TryGuessFromPackage_IoPackage_ReturnsReversed()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("io.example.app");
            Assert.AreEqual("example.io", result);
        }

        [TestMethod]
        public void TryGuessFromPackage_SinglePart_ReturnsNull()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("example");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryGuessFromPackage_UnknownPrefix_ReturnsNull()
        {
            string result = AndroidAppMapper.TryGuessFromPackage("uk.co.example");
            Assert.IsNull(result);
        }
    }
}
