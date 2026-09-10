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
        public void ReleaseCandidate_AssemblyAndUpdateFeedAgreeOnVersion13()
        {
            var expected = new Version(1, 3, 0, 0);
            Assembly assembly = typeof(FetchProfileCatalog).Assembly;
            Assert.AreEqual(expected, assembly.GetName().Version,
                "The final candidate must use the v1.3 assembly version.");
            var fileVersion = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyFileVersionAttribute));
            Assert.IsNotNull(fileVersion);
            Assert.AreEqual(expected, new Version(fileVersion.Version));
            string[] feedVersions = SourceFile("version.txt").Split('\n')
                .Select(line => line.Trim()).Where(line => Regex.IsMatch(line, @"^\d+\.\d+\.\d+\.\d+$")).ToArray();
            Assert.AreEqual(1, feedVersions.Length, "The update feed must contain one version.");
            Assert.AreEqual(expected, new Version(feedVersions[0]));
            StringAssert.Contains(SourceFile("CHANGELOG.md"), "## [1.3.0]");
        }

        [TestMethod]
        public void Website_IdentifiesCandidateWithoutAdvertisingAnUnpublishedStableAsset()
        {
            var serializer = new JavaScriptSerializer();
            var release = serializer.Deserialize<Dictionary<string, object>>(
                SourceFile("site/data/release.json"));
            string stable = (string)release["version"];
            string preview = release.ContainsKey("previewVersion") ? release["previewVersion"] as string : null;
            Assert.AreEqual("1.3.0", string.IsNullOrEmpty(preview) ? stable : preview,
                "The source candidate must be identified independently of the published stable download.");
            Assert.IsTrue(new Version(stable) <= new Version(1, 3, 0));
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
            StringAssert.Contains(readme, "v1.3");
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
        }
    }
}
