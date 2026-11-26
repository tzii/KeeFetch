using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using KeeFetch.FetchProfiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class VersionConsistencyTests
    {
        private static string SourceFile(string relative)
        {
            string root = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relative));
        }

        [TestMethod]
        public void ReleaseCandidate_AssemblyAndUpdateFeedAgreeOnVersion14()
        {
            var expected = new Version(1, 4, 0, 0);
            Assembly assembly = typeof(FetchProfileCatalog).Assembly;
            Assert.AreEqual(expected, assembly.GetName().Version,
                "The candidate must use the v1.4 assembly version.");
            var fileVersion = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyFileVersionAttribute));
            Assert.IsNotNull(fileVersion);
            Assert.AreEqual(expected, new Version(fileVersion.Version));
            StringAssert.Contains(SourceFile("CHANGELOG.md"), "## [1.4.0]");
        }

        [TestMethod]
        public void UpdateFeed_UsesKeePassVersionInformationFormat()
        {
            // KeePass reads "<separator>" / "<AssemblyTitle><separator><file version>" /
            // "<separator>"; any other layout makes the update check fail silently.
            Assembly assembly = typeof(FetchProfileCatalog).Assembly;
            var title = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyTitleAttribute));
            var fileVersion = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyFileVersionAttribute));
            Assert.IsNotNull(title);
            Assert.IsNotNull(fileVersion);

            string raw = SourceFile("version.txt");
            Assert.IsFalse(raw.Length > 0 && raw[0] == (char)0xFEFF,
                "KeePass before 2.21 cannot read a byte order mark.");
            string[] lines = raw.Split('\n').Select(line => line.Trim())
                .Where(line => line.Length > 0).ToArray();

            CollectionAssert.AreEqual(
                new[] { ":", title.Title + ":" + fileVersion.Version, ":" },
                lines);
        }

        [TestMethod]
        public void Website_IdentifiesCandidateWithoutAdvertisingAnUnpublishedStableAsset()
        {
            var serializer = new JavaScriptSerializer();
            var release = serializer.Deserialize<Dictionary<string, object>>(
                SourceFile("site/data/release.json"));
            string stable = (string)release["version"];
            string preview = release.ContainsKey("previewVersion") ? release["previewVersion"] as string : null;
            Assert.AreEqual("1.4.0", string.IsNullOrEmpty(preview) ? stable : preview,
                "The source candidate must be identified independently of the published stable download.");
            Assert.IsTrue(new Version(stable) <= new Version(1, 4, 0));
            Assert.AreEqual("v" + stable, release["tag"]);
            Assert.AreEqual("https://github.com/tzii/KeeFetch/releases/tag/v" + stable, release["releaseUrl"]);
            foreach (string format in new[] { "dll", "plgx" })
                Assert.AreEqual("https://github.com/tzii/KeeFetch/releases/download/v" + stable + "/KeeFetch." + format,
                    release[format + "Url"]);
        }

        [TestMethod]
        public void CurrentProfileClaims_UseManagedNamesAndBudgets()
        {
            string readme = SourceFile("README.md");
            StringAssert.Contains(readme, "v1.4");
            var serializer = new JavaScriptSerializer();
            var export = serializer.Deserialize<Dictionary<string, object>>(SourceFile("site/data/profiles.json"));
            var profiles = ((ArrayList)export["profiles"]).Cast<Dictionary<string, object>>().ToArray();
            foreach (FetchProfileDefinition profile in FetchProfileCatalog.ManagedProfiles.Where(p => p.IsVisible))
            {
                var row = profiles.Single(p => (string)p["id"] == profile.Id);
                Assert.AreEqual(profile.DisplayName, row["displayName"]);
                string pattern = @"^\|\s+\*\*" + Regex.Escape(profile.DisplayName) + @"\*\*(?: \(default\))?\s*\|";
                string tableRow = readme.Split('\n').Single(line => Regex.IsMatch(line, pattern));
                Assert.AreEqual((profile.CumulativeTimeoutMs / 1000) + " s", tableRow.Split('|')[3].Trim());
            }
            Assert.IsFalse(profiles.Any(p => (string)p["displayName"] == "Thorough"),
                "Thorough is a legacy migration value, not a current managed profile name.");
            CollectionAssert.AreEqual(new[] { "everyday", "privacy" },
                profiles.Select(p => (string)p["id"]).ToArray(),
                "v1.4 offers Balanced and Privacy; Fast and Precise are retired.");
        }
    }
}
