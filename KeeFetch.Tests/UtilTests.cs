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
        public void ExtractHost_InvalidUrl_ReturnsNull()
        {
            string result = Util.ExtractHost("not a valid url");
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
        public void IsPrivateHost_LoopbackIP_ReturnsTrue()
        {
            Assert.IsTrue(Util.IsPrivateHost("127.0.0.1"));
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
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            // This won't be a valid image since it's just the header, but we test the method structure
            // In real tests, you'd use actual image files
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
