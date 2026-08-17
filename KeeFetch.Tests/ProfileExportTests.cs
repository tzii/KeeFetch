using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using KeeFetch.FetchProfiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class ProfileExportTests
    {
        private static string ProfilesJsonPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            return Path.Combine(repoRoot, "site", "data", "profiles.json");
        }

        private static Dictionary<string, object> LoadExportRoot()
        {
            string path = ProfilesJsonPath();
            Assert.IsTrue(File.Exists(path),
                "site/data/profiles.json is missing. Run eng/export-profile-data.ps1 to generate it.");
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        }

        [TestMethod]
        public void ProfilesJson_MatchesManagedCatalog()
        {
            Dictionary<string, object> root = LoadExportRoot();

            Assert.AreEqual(1, Convert.ToInt32(root["schema"]), "Unexpected export schema version.");
            Assert.AreEqual("FetchProfileCatalog.ManagedProfiles", (string)root["source"],
                "Export source must name the managed catalog.");

            ArrayList exported = (ArrayList)root["profiles"];
            List<FetchProfileDefinition> visible = FetchProfileCatalog.ManagedProfiles
                .Where(p => p.IsVisible)
                .ToList();

            Assert.AreEqual(visible.Count, exported.Count,
                "Exported profile count must match visible managed profiles.");

            for (int i = 0; i < visible.Count; i++)
            {
                FetchProfileDefinition expected = visible[i];
                Dictionary<string, object> actual = (Dictionary<string, object>)exported[i];

                Assert.AreEqual(expected.Id, (string)actual["id"],
                    "Profile order or id mismatch at index " + i + ".");
                Assert.AreEqual(expected.DisplayName, (string)actual["displayName"],
                    "Display name mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.Description, (string)actual["description"],
                    "Description mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.IntendedUse, (string)actual["intendedUse"],
                    "Intended use mismatch for " + expected.Id + ".");

                ArrayList providerIds = (ArrayList)actual["providerIds"];
                CollectionAssert.AreEqual(expected.ProviderIds.ToArray(),
                    providerIds.Cast<string>().ToArray(),
                    "Provider chain mismatch for " + expected.Id + ".");

                Assert.AreEqual(expected.PrimaryTimeoutMs, Convert.ToInt32(actual["primaryTimeoutMs"]),
                    "Primary timeout mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.FallbackTimeoutMs, Convert.ToInt32(actual["fallbackTimeoutMs"]),
                    "Fallback timeout mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.CumulativeTimeoutMs, Convert.ToInt32(actual["cumulativeTimeoutMs"]),
                    "Cumulative timeout mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.AllowSyntheticFallbacks, Convert.ToBoolean(actual["allowSyntheticFallbacks"]),
                    "Synthetic fallback flag mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.IsVisible, Convert.ToBoolean(actual["isVisible"]),
                    "Visibility mismatch for " + expected.Id + ".");
                Assert.AreEqual(expected.EvidenceReport, (string)actual["evidenceReport"],
                    "Evidence report mismatch for " + expected.Id + ".");
            }
        }

        [TestMethod]
        public void ProfilesJson_UsesUtf8WithoutBom()
        {
            byte[] raw = File.ReadAllBytes(ProfilesJsonPath());
            Assert.IsTrue(raw.Length >= 3, "Export file is unexpectedly empty.");
            Assert.IsFalse(raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
                "Export must be UTF-8 without a byte-order mark.");
        }
    }
}
