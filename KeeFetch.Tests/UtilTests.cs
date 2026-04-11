using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class UtilTests
    {
        [TestMethod]
        public void HashData_Returns16ByteHash()
        {
            byte[] input = System.Text.Encoding.UTF8.GetBytes("test data");
            byte[] hash = Util.HashData(input);
            
            Assert.IsNotNull(hash);
            Assert.AreEqual(16, hash.Length);
        }

        [TestMethod]
        public void HashData_SameInput_ReturnsSameHash()
        {
            byte[] input = System.Text.Encoding.UTF8.GetBytes("test data");
            byte[] hash1 = Util.HashData(input);
            byte[] hash2 = Util.HashData(input);
            
            CollectionAssert.AreEqual(hash1, hash2);
        }

        [TestMethod]
        public void HashData_DifferentInput_ReturnsDifferentHash()
        {
            byte[] input1 = System.Text.Encoding.UTF8.GetBytes("test data 1");
            byte[] input2 = System.Text.Encoding.UTF8.GetBytes("test data 2");
            byte[] hash1 = Util.HashData(input1);
            byte[] hash2 = Util.HashData(input2);
            
            CollectionAssert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void ExtractHost_WithScheme_ReturnsHost()
        {
            string result = Util.ExtractHost("https://example.com/path");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void ExtractHost_WithoutScheme_ReturnsHost()
        {
            string result = Util.ExtractHost("example.com/path");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void ExtractHost_WithPort_ReturnsHostWithoutPort()
        {
            string result = Util.ExtractHost("https://example.com:8080/path");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void ExtractHost_NullUrl_ReturnsNull()
        {
            string result = Util.ExtractHost(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractHost_EmptyUrl_ReturnsNull()
        {
            string result = Util.ExtractHost("");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractHost_WhitespaceUrl_ReturnsNull()
        {
            string result = Util.ExtractHost("   ");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractHostWithPort_NullUrl_ReturnsNull()
        {
            string result = Util.ExtractHostWithPort(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractScheme_NullUrl_ReturnsNull()
        {
            string result = Util.ExtractScheme(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void HashData_Null_ReturnsNull()
        {
            byte[] result = Util.HashData(null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractHostWithPort_DefaultPort_ReturnsHostOnly()
        {
            string result = Util.ExtractHostWithPort("https://example.com/path");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void ExtractHostWithPort_NonDefaultPort_ReturnsHostWithPort()
        {
            string result = Util.ExtractHostWithPort("https://example.com:8080/path");
            Assert.AreEqual("example.com:8080", result);
        }

        [TestMethod]
        public void ExtractScheme_WithScheme_ReturnsScheme()
        {
            string result = Util.ExtractScheme("https://example.com");
            Assert.AreEqual("https", result);
        }

        [TestMethod]
        public void ExtractScheme_WithoutScheme_ReturnsNull()
        {
            string result = Util.ExtractScheme("example.com");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryParseHttpUri_WithPrefixEnabled_ParsesBareHost()
        {
            System.Uri uri;
            bool ok = Util.TryParseHttpUri("example.com/login", true, out uri);
            Assert.IsTrue(ok);
            Assert.AreEqual("example.com", uri.Host);
            Assert.AreEqual("https", uri.Scheme);
        }

        [TestMethod]
        public void GetNormalizedOriginKey_DistinguishesSchemeAndPort()
        {
            string https = Util.GetNormalizedOriginKey("https://example.com", true);
            string http = Util.GetNormalizedOriginKey("http://example.com", true);
            string custom = Util.GetNormalizedOriginKey("https://example.com:8443", true);

            Assert.AreEqual("https://example.com:443", https);
            Assert.AreEqual("http://example.com:80", http);
            Assert.AreEqual("https://example.com:8443", custom);
            Assert.AreNotEqual(https, http);
            Assert.AreNotEqual(https, custom);
        }

        [TestMethod]
        public void IsPrivateHost_Localhost_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost("localhost"));
        }

        [TestMethod]
        public void IsPrivateHost_LocalDotLocal_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost("server.local"));
        }

        [TestMethod]
        public void IsPrivateHost_PrivateIP_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost("192.168.1.1"));
            Assert.IsTrue(Util.IsPrivateHost("10.0.0.1"));
            Assert.IsTrue(Util.IsPrivateHost("172.16.0.1"));
        }

        [TestMethod]
        public void IsPrivateHost_PublicIP_ReturnsFalse()
        {
            Assert.IsFalse(Util.IsPrivateHost("8.8.8.8"));
            Assert.IsFalse(Util.IsPrivateHost("1.1.1.1"));
        }

        [TestMethod]
        public void IsPrivateHost_PublicDomain_ReturnsFalse()
        {
            Assert.IsFalse(Util.IsPrivateHost("google.com"));
            Assert.IsFalse(Util.IsPrivateHost("example.com"));
        }

        [TestMethod]
        public void IsPrivateHost_Null_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost(null));
        }

        [TestMethod]
        public void IsPrivateHost_Empty_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost(""));
        }

        [TestMethod]
        public void GuessDomainFromTitle_SimpleName_ReturnsWithCom()
        {
            string result = Util.GuessDomainFromTitle("example");
            Assert.AreEqual("example.com", result);
        }

        [TestMethod]
        public void GuessDomainFromTitle_WithUrl_ReturnsAsIs()
        {
            string result = Util.GuessDomainFromTitle("https://example.com");
            Assert.AreEqual("https://example.com", result);
        }

        [TestMethod]
        public void GuessDomainFromTitle_InternalSuffix_ReturnsNull()
        {
            string result = Util.GuessDomainFromTitle("myapp-internal");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GuessDomainFromTitle_Empty_ReturnsNull()
        {
            string result = Util.GuessDomainFromTitle("");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void IsValidImage_ValidPng_ReturnsTrue()
        {
            // PNG file header: 89 50 4E 47 0D 0A 1A 0A
            // Create a minimal valid PNG (1x1 pixel)
            byte[] minimalPng = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 pixel
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, // IHDR CRC
                0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, // IDAT chunk
                0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00, 0x01,
                0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4, // IDAT data + CRC
                0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
                0xAE, 0x42, 0x60, 0x82 // IEND CRC
            };
            
            bool result = Util.IsValidImage(minimalPng);
            Assert.IsTrue(result, "Minimal valid PNG should be recognized as valid image");
        }

        [TestMethod]
        public void IsValidImage_Null_ReturnsFalse()
        {
            Assert.IsFalse(Util.IsValidImage(null));
        }

        [TestMethod]
        public void IsValidImage_TooShort_ReturnsFalse()
        {
            byte[] data = new byte[] { 0x01, 0x02, 0x03 };
            Assert.IsFalse(Util.IsValidImage(data));
        }
    }
}
